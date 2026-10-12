const intervalMs = 5000;
const activeAttribute = "data-active";

class UnaykaCarousel extends HTMLElement {
    connectedCallback() {
        this.slides = [...this.querySelectorAll("[data-slide]")];
        this.bullets = [...this.querySelectorAll("[data-bullet]")];
        this.index = 0;
        this.bullets.forEach((bullet, index) => bullet.addEventListener("click", () => this.show(index, true)));
        this.addEventListener("mouseenter", () => this.stop());
        this.addEventListener("mouseleave", () => this.start());
        this.show(0, false);
        this.start();
    }

    disconnectedCallback() {
        this.stop();
    }

    start() {
        this.stop();
        if (this.slides.length > 1) {
            this.timer = setInterval(() => this.show((this.index + 1) % this.slides.length, false), intervalMs);
        }
    }

    stop() {
        clearInterval(this.timer);
    }

    show(index, restart) {
        this.index = index;
        this.slides.forEach((slide, position) => slide.toggleAttribute(activeAttribute, position === index));
        this.bullets.forEach((bullet, position) => bullet.setAttribute("aria-current", position === index ? "true" : "false"));
        if (restart) {
            this.start();
        }
    }
}

if (!customElements.get("unayka-carousel")) {
    customElements.define("unayka-carousel", UnaykaCarousel);
}
