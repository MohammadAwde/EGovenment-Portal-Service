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
    document.querySelectorAll(".lb-navbar .nav-link").forEach(function (link) {
      var href = link.getAttribute("href");
      if (href && href !== "/" && path.startsWith(href.toLowerCase())) {
        link.classList.add("active");
      }
    });
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

  function init() {
    initScrollToTop();
    animateCounters();
    initAlertAutoDismiss();
    highlightActiveNav();
    initPaymentMethodToggle();
    initFormLoadingStates();
    initCardAnimations();
    initTooltips();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
