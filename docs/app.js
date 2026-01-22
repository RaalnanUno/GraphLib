(() => {
  /* ---------------------------------------------
   * Copy-to-clipboard for code blocks
   * ------------------------------------------- */
  function initCopyButtons() {
    document.querySelectorAll("[data-copy]").forEach(btn => {
      btn.addEventListener("click", async () => {
        const selector = btn.getAttribute("data-copy");
        const target = document.querySelector(selector);
        if (!target) return;

        try {
          await navigator.clipboard.writeText(target.innerText);

          const originalHtml = btn.innerHTML;
          btn.innerHTML = '<i class="bi bi-check2 me-1"></i>Copied';
          btn.classList.remove("btn-outline-primary");
          btn.classList.add("btn-success");

          setTimeout(() => {
            btn.innerHTML = originalHtml;
            btn.classList.add("btn-outline-primary");
            btn.classList.remove("btn-success");
          }, 1200);
        } catch {
          alert("Copy failed. Your browser may block clipboard access.");
        }
      });
    });
  }

  /* ---------------------------------------------
   * Accordion expand / collapse helpers
   * ------------------------------------------- */
  function initAccordionControls(accordionId) {
    const accordion =
      accordionId
        ? document.getElementById(accordionId)
        : document.querySelector(".accordion");

    if (!accordion) return;

    const items = accordion.querySelectorAll(".accordion-collapse");

    document.getElementById("btnExpandAll")?.addEventListener("click", () => {
      items.forEach(el => new bootstrap.Collapse(el, { show: true }));
    });

    document.getElementById("btnCollapseAll")?.addEventListener("click", () => {
      items.forEach(el => new bootstrap.Collapse(el, { hide: true }));
    });
  }

  /* ---------------------------------------------
   * Print handler
   * ------------------------------------------- */
  function initPrintButton() {
    document
      .getElementById("btnPrint")
      ?.addEventListener("click", () => window.print());
  }

  /* ---------------------------------------------
   * Bootstrapping
   * ------------------------------------------- */
  document.addEventListener("DOMContentLoaded", () => {
    initCopyButtons();

    // Works for either page; change if you want strict IDs
    initAccordionControls("accordionTroubleshooting");
    initAccordionControls("accordionGettingStarted");

    initPrintButton();
  });
})();
