document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-toast]").forEach((toast, index) => {
    window.setTimeout(() => {
      toast.classList.add("is-visible");
    }, 120 * index);

    window.setTimeout(() => {
      toast.classList.remove("is-visible");
      toast.classList.add("is-leaving");
    }, 4200 + 120 * index);

    window.setTimeout(() => {
      toast.remove();
    }, 5000 + 120 * index);
  });
});
