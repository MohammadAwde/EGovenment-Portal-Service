/**
 * Lebanese E-Government Portal — UI Interactions
 */
;(function () {
  "use strict";

  // Scroll-to-top
  function initScrollToTop() {
    var btn = document.getElementById("scrollTopBtn");
    if (!btn) return;
    window.addEventListener("scroll", function () {
      btn.classList.toggle("visible", window.scrollY > 300);
    });
    btn.addEventListener("click", function () {
      window.scrollTo({ top: 0, behavior: "smooth" });
    });
  }

  // Animated counters
  function animateCounters() {
    var counters = document.querySelectorAll("[data-counter]");
    if (!counters.length) return;
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        var el = entry.target;
        var target = parseInt(el.getAttribute("data-counter") || "0", 10);
        if (isNaN(target)) return;
        var current = 0;
        var step = Math.max(1, Math.ceil(target / 40));
        var interval = setInterval(function () {
          current += step;
          if (current >= target) { current = target; clearInterval(interval); }
          el.textContent = current.toLocaleString();
        }, 30);
        observer.unobserve(el);
      });
    }, { threshold: 0.3 });
    counters.forEach(function (c) { observer.observe(c); });
  }

  // Auto-dismiss alerts
  function initAlertAutoDismiss() {
    document.querySelectorAll(".alert-dismissible").forEach(function (alert) {
      setTimeout(function () {
        var btn = alert.querySelector(".btn-close");
        if (btn) btn.click();
      }, 6000);
    });
  }

  // Active nav link
    function highlightActiveNav() {
        var path = window.location.pathname.toLowerCase();
        var links = document.querySelectorAll(".lb-navbar .nav-link");
        var bestMatch = null;
        var bestLength = -1;

        links.forEach(function (link) {
            var href = link.getAttribute("href");
            if (!href || href === "/") return;
            href = href.toLowerCase();
            var matches = (path === href) || path.startsWith(href + "/");
            if (matches && href.length > bestLength) {
                bestMatch = link;
                bestLength = href.length;
            }
        });

        if (bestMatch) bestMatch.classList.add("active");
    }

  // Payment method toggle highlight
  function initPaymentMethodToggle() {
    var radios = document.querySelectorAll('input[name="method"]');
    if (!radios.length) return;
    radios.forEach(function (radio) {
      radio.addEventListener("change", function () {
        document.querySelectorAll(".list-group-item").forEach(function (item) {
          item.classList.remove("border-success", "bg-light");
        });
        var parent = radio.closest(".list-group-item");
        if (parent) parent.classList.add("border-success", "bg-light");
      });
    });
    var checked = document.querySelector('input[name="method"]:checked');
    if (checked) {
      var parent = checked.closest(".list-group-item");
      if (parent) parent.classList.add("border-success", "bg-light");
    }
  }

  // Submit button loading state
  function initFormLoadingStates() {
    document.querySelectorAll("form").forEach(function (form) {
      form.addEventListener("submit", function () {
        var btn = form.querySelector('button[type="submit"]');
        if (btn && !btn.disabled) {
          btn.disabled = true;
          var original = btn.innerHTML;
          btn.innerHTML = '<span class="lb-spinner me-2"></span>Processing\u2026';
          setTimeout(function () { btn.disabled = false; btn.innerHTML = original; }, 8000);
        }
      });
    });
  }

  // Card entrance animation
  function initCardAnimations() {
    var cards = document.querySelectorAll(".card, .lb-service-card, .lb-stat-card");
    if (!cards.length) return;
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.style.opacity = "1";
          entry.target.style.transform = "translateY(0)";
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.1 });
    cards.forEach(function (card) {
      card.style.opacity = "0";
      card.style.transform = "translateY(20px)";
      card.style.transition = "opacity 0.5s ease, transform 0.5s ease";
      observer.observe(card);
    });
  }

  // Bootstrap tooltips
  function initTooltips() {
    var triggers = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    triggers.forEach(function (el) {
      if (window.bootstrap && window.bootstrap.Tooltip) {
        new window.bootstrap.Tooltip(el);
      }
    });
  }

  // Dark mode toggle
  function initDarkMode() {
    var toggle = document.getElementById('toggleDarkMode');
    if (!toggle) return;
      function apply(isDark) {
          if (isDark) document.body.classList.add('dark-theme'); else document.body.classList.remove('dark-theme');
          var icon = toggle.querySelector('i');
          if (icon) icon.className = isDark ? 'bi bi-sun-fill' : 'bi bi-moon-fill';
          var label = toggle.querySelector('span');
          if (label) label.textContent = isDark ? 'Light' : 'Dark';
          try { localStorage.setItem('smartegov:dark', isDark ? '1' : '0'); } catch (e) { }
      }
    // initial state
    try {
      var stored = localStorage.getItem('smartegov:dark');
      if (stored === null) {
        // follow system preference
        var prefers = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        apply(prefers);
      } else {
        apply(stored === '1');
      }
    } catch(e) { apply(false); }

    toggle.addEventListener('click', function () {
      var isDark = document.body.classList.contains('dark-theme');
      apply(!isDark);
    });
  }

  function init() {
    initScrollToTop();
    animateCounters();
    initAlertAutoDismiss();
    highlightActiveNav();
    initPaymentMethodToggle();
    initFormLoadingStates();
    initCardAnimations();
    initTooltips();
    initDarkMode();
    initNotificationHandlers();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }

  // Notification handlers: intercept mark-as-read forms and submit via fetch to avoid full reloads
  function initNotificationHandlers() {
    document.addEventListener('submit', function (e) {
      var form = e.target;
      if (!form || !form.classList.contains('mark-as-read-form')) return;
      e.preventDefault();

      var btn = form.querySelector('button[type="submit"]');
      if (btn) { btn.disabled = true; }

      var formData = new FormData(form);
      var action = form.getAttribute('action') || window.location.pathname;

      fetch(action, {
        method: 'POST',
        body: formData,
        credentials: 'same-origin',
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
      }).then(function (res) {
        if (res.ok) {
          // remove the notification element from DOM using stable data attribute
          try {
            var idInput = form.querySelector('input[name="id"]');
            var id = idInput ? idInput.value : form.getAttribute('data-notification-id');
            if (id) {
              var selector = '.lb-notification-item[data-notification-id="' + id + '"]';
              var itemById = document.querySelector(selector);
              if (itemById) itemById.remove();
            }
          } catch (e) { /* ignore and fallback */ }

          // update nav badge(s)
            var badge = document.getElementById('notification-unread-count');
            var navBadge = document.getElementById('notifBadge');
          try {
            if (badge) {
                // try to decrement current numeric value safely
                var cur = parseInt(badge.textContent.replace(/[^0-9]/g, '') || '0', 10) || 0;
                var next = Math.max(0, cur - 1);
              if (next > 0) {
                badge.textContent = next;
                badge.style.display = 'inline-block';
              } else {
                badge.style.display = 'none';
              }
            }
            if (navBadge) {
              // navBadge may contain spinner; fetch current count from server
              fetch('/Notification/UnreadCount', { credentials: 'same-origin' })
                .then(function (r) { return r.json(); })
                .then(function (count) {
                  navBadge.innerHTML = count;
                }).catch(function () { /* ignore */ });
            }
          } catch (ex) { /* ignore */ }
        } else {
          // fallback: on failure, reload so user sees correct state
          window.location.reload();
        }
      }).catch(function () {
        window.location.reload();
      }).finally(function () { if (btn) btn.disabled = false; });
    });
  }
})();
