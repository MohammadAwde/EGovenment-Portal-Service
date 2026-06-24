/**
 * Lebanese E-Government Portal — UI Interactions
 * Compiled as plain ES2020 JS (TypeScript-style, no build step needed for ASP.NET)
 */

;(function () {
  "use strict";

  // ?? Scroll-to-top button ?????????????????????????????
  function initScrollToTop(): void {
    const btn = document.getElementById("scrollTopBtn");
    if (!btn) return;

    window.addEventListener("scroll", () => {
      btn.classList.toggle("visible", window.scrollY > 300);
    });

    btn.addEventListener("click", () => {
      window.scrollTo({ top: 0, behavior: "smooth" });
    });
  }

  // ?? Animated counters ????????????????????????????????
  function animateCounters(): void {
    const counters = document.querySelectorAll<HTMLElement>("[data-counter]");
    if (!counters.length) return;

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          const el = entry.target as HTMLElement;
          const target = parseInt(el.getAttribute("data-counter") || "0", 10);
          if (isNaN(target)) return;

          let current = 0;
          const step = Math.max(1, Math.ceil(target / 40));
          const interval = setInterval(() => {
            current += step;
            if (current >= target) {
              current = target;
              clearInterval(interval);
            }
            el.textContent = current.toLocaleString();
          }, 30);

          observer.unobserve(el);
        });
      },
      { threshold: 0.3 }
    );

    counters.forEach((c) => observer.observe(c));
  }

  // ?? Auto-dismiss alerts ??????????????????????????????
  function initAlertAutoDismiss(): void {
    document.querySelectorAll<HTMLElement>(".alert-dismissible").forEach((alert) => {
      setTimeout(() => {
        const closeBtn = alert.querySelector<HTMLButtonElement>(".btn-close");
        if (closeBtn) closeBtn.click();
      }, 6000);
    });
  }

  // ?? Active nav link highlight ????????????????????????
  function highlightActiveNav(): void {
    const path = window.location.pathname.toLowerCase();
    document.querySelectorAll<HTMLAnchorElement>(".lb-navbar .nav-link").forEach((link) => {
      const href = link.getAttribute("href");
      if (href && href !== "/" && path.startsWith(href.toLowerCase())) {
        link.classList.add("active");
      }
    });
  }

  // ?? Payment method toggle ????????????????????????????
  function initPaymentMethodToggle(): void {
    const radios = document.querySelectorAll<HTMLInputElement>('input[name="method"]');
    if (!radios.length) return;

    radios.forEach((radio) => {
      radio.addEventListener("change", () => {
        document.querySelectorAll<HTMLElement>(".list-group-item").forEach((item) => {
          item.classList.remove("border-success", "bg-light");
        });
        const parent = radio.closest(".list-group-item");
        if (parent) {
          parent.classList.add("border-success", "bg-light");
        }
      });
    });

    // Highlight default selection
    const checked = document.querySelector<HTMLInputElement>('input[name="method"]:checked');
    if (checked) {
      const parent = checked.closest(".list-group-item");
      if (parent) parent.classList.add("border-success", "bg-light");
    }
  }

  // ?? Button loading state ?????????????????????????????
  function initFormLoadingStates(): void {
    document.querySelectorAll<HTMLFormElement>("form").forEach((form) => {
      form.addEventListener("submit", () => {
        const btn = form.querySelector<HTMLButtonElement>('button[type="submit"]');
        if (btn && !btn.disabled) {
          btn.disabled = true;
          const originalText = btn.innerHTML;
          btn.innerHTML = '<span class="lb-spinner me-2"></span>Processing...';
          // Restore after 8s in case of redirect failure
          setTimeout(() => {
            btn.disabled = false;
            btn.innerHTML = originalText;
          }, 8000);
        }
      });
    });
  }

  // ?? Smooth card entrance animation ???????????????????
  function initCardAnimations(): void {
    const cards = document.querySelectorAll<HTMLElement>(".card, .lb-service-card, .lb-stat-card");
    if (!cards.length) return;

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            (entry.target as HTMLElement).style.opacity = "1";
            (entry.target as HTMLElement).style.transform = "translateY(0)";
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.1 }
    );

    cards.forEach((card) => {
      card.style.opacity = "0";
      card.style.transform = "translateY(20px)";
      card.style.transition = "opacity 0.5s ease, transform 0.5s ease";
      observer.observe(card);
    });
  }

  // ?? Tooltip init (Bootstrap) ?????????????????????????
  function initTooltips(): void {
    const tooltipTriggers = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipTriggers.forEach((el) => {
      new (window as any).bootstrap.Tooltip(el);
    });
  }

  // ?? Init all ?????????????????????????????????????????
  function init(): void {
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
