// Avatisment — feed interactions (like, comment, follow) via fetch, no page reloads.
(function () {
  function tokenFromForm(form) {
    var el = form ? form.querySelector('input[name="__RequestVerificationToken"]') : null;
    if (el) return el.value;
    var any = document.querySelector('input[name="__RequestVerificationToken"]');
    return any ? any.value : "";
  }

  async function postForm(url, data) {
    var body = new URLSearchParams(data);
    var res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: body.toString()
    });
    if (!res.ok) throw new Error("Request failed: " + res.status);
    return res.json();
  }

  var csrfToken = document.querySelector('input[name="__RequestVerificationToken"]');
  var token = csrfToken ? csrfToken.value : "";

  // ---- Like ----
  document.addEventListener("click", async function (e) {
    var btn = e.target.closest(".like-btn");
    if (!btn) return;
    var postId = btn.dataset.postId;
    if (!postId) return;

    btn.classList.add("pulse");
    setTimeout(function () { btn.classList.remove("pulse"); }, 340);

    try {
      var result = await postForm("/Home/ToggleLike", { postId: postId, __RequestVerificationToken: token });
      btn.classList.toggle("liked", result.liked);
      var countEl = btn.querySelector(".like-count");
      if (countEl) countEl.textContent = result.count;

      // keep the "N likes" summary line in sync if present on the card
      var card = btn.closest(".post-card");
      if (card) {
        var stats = card.querySelector(".post-stats span");
        if (stats) stats.textContent = result.count + " likes";
      }
    } catch (err) {
      console.error(err);
    }
  });

  // ---- Toggle comment panel ----
  document.addEventListener("click", function (e) {
    var btn = e.target.closest(".comment-toggle");
    if (!btn) return;
    var panel = document.getElementById("comments-" + btn.dataset.postId);
    if (panel) panel.hidden = !panel.hidden;
  });

  // ---- Add comment ----
  document.addEventListener("submit", async function (e) {
    var form = e.target.closest(".comment-form");
    if (!form) return;
    e.preventDefault();

    var input = form.querySelector("input");
    var content = input.value.trim();
    if (!content) return;

    try {
      var result = await postForm("/Home/AddComment", {
        postId: form.dataset.postId,
        Content: content,
        __RequestVerificationToken: token
      });
      if (!result.ok) return;

      var list = form.closest(".comment-section").querySelector(".comment-list");
      var row = document.createElement("div");
      row.className = "comment-row";
      row.innerHTML =
        '<div class="avatar avatar-xs" style="background:' + (result.avatarColor || "#7C5CFC") + '">' +
        (result.initials || "") + "</div>" +
        '<div class="comment-bubble"><strong>' + escapeHtml(result.author || "") +
        "</strong><span>" + escapeHtml(result.content || "") + "</span></div>";
      list.appendChild(row);
      input.value = "";

      // bump the visible comment counters
      var card = form.closest(".post-card");
      if (card) {
        var toggleCount = card.querySelector(".comment-toggle span");
        if (toggleCount) toggleCount.textContent = (parseInt(toggleCount.textContent || "0", 10) + 1);
        var statSpans = card.querySelectorAll(".post-stats span");
        if (statSpans[1]) {
          var n = parseInt(statSpans[1].textContent, 10) || 0;
          statSpans[1].textContent = (n + 1) + " comments";
        }
      }
    } catch (err) {
      console.error(err);
    }
  });

  // ---- Follow / unfollow ----
  document.addEventListener("click", async function (e) {
    var btn = e.target.closest(".follow-btn, .profile-follow-btn");
    if (!btn) return;
    var userId = btn.dataset.userId;
    if (!userId) return;

    try {
      var result = await postForm("/Home/ToggleFollow", { targetUserId: userId, __RequestVerificationToken: token });
      btn.classList.toggle("following", result.following);
      btn.textContent = result.following ? "Following" : "Follow";

      // on a profile page, reveal/hide the Message button as follow state changes
      var actions = btn.closest(".profile-actions");
      if (actions) {
        var existingMsg = actions.querySelector(".message-btn");
        if (result.following && !existingMsg) {
          var handle = actions.dataset.profileHandle;
          var link = document.createElement("a");
          link.className = "btn-ghost message-btn";
          link.href = "/Home/Messages?handle=" + encodeURIComponent(handle);
          link.innerHTML = '<svg viewBox="0 0 24 24"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg> Message';
          actions.appendChild(link);
        } else if (!result.following && existingMsg) {
          existingMsg.remove();
        }
      }
    } catch (err) {
      console.error(err);
    }
  });

  // ---- Messages: send + append ----
  document.addEventListener("submit", async function (e) {
    var form = e.target.closest("#threadComposer");
    if (!form) return;
    e.preventDefault();

    var input = document.getElementById("threadInput");
    var content = input.value.trim();
    if (!content) return;

    var formToken = tokenFromForm(form);
    try {
      var result = await postForm("/Home/SendMessage", {
        toUserId: form.dataset.toUserId,
        content: content,
        __RequestVerificationToken: formToken
      });
      if (!result.ok) return;

      var thread = document.getElementById("threadMessages");
      var bubble = document.createElement("div");
      bubble.className = "thread-msg mine";
      bubble.innerHTML = "<span>" + escapeHtml(result.content) + "</span>";
      thread.appendChild(bubble);
      thread.scrollTop = thread.scrollHeight;
      input.value = "";

      // reflect the new message as the conversation preview in the sidebar list
      var activeRow = document.querySelector(".conv-row.active .conv-info span");
      if (activeRow) activeRow.textContent = result.content;
    } catch (err) {
      console.error(err);
    }
  });

  // scroll message thread to bottom on load
  document.addEventListener("DOMContentLoaded", function () {
    var thread = document.getElementById("threadMessages");
    if (thread) thread.scrollTop = thread.scrollHeight;
  });

  function escapeHtml(str) {
    var div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
  }
})();
