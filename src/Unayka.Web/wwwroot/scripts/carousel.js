const heroIntervalMs = 5000;
const fadeIntervalMs = 4000;
const railIntervalMs = 4000;
const railResumeDelayMs = 6000;
const swipeThresholdPx = 50;
const dragThresholdPx = 6;
const copiedResetMs = 2000;
const desktopQuery = "(min-width: 768px)";
const activeAttribute = "data-active";
const draggingAttribute = "data-dragging";

const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

function define(name, type) {
    if (!customElements.get(name)) {
        customElements.define(name, type);
    }
}

function suppressNextClick(element) {
    const block = event => {
        event.preventDefault();
        event.stopPropagation();
    };
    element.addEventListener("click", block, { capture: true, once: true });
    setTimeout(() => element.removeEventListener("click", block, { capture: true }), 0);
}

class UnaykaCarousel extends HTMLElement {
    connectedCallback() {
        this.slides = [...this.querySelectorAll("[data-slide]")];
        this.bullets = [...this.querySelectorAll("[data-bullet]")];
        this.index = 0;
        this.bullets.forEach((bullet, index) => bullet.addEventListener("click", () => this.show(index, true)));
        this.addEventListener("mouseenter", () => this.stop());
        this.addEventListener("mouseleave", () => this.start());
        this.addEventListener("focusin", () => this.stop());
        this.addEventListener("focusout", () => this.start());
        this.addEventListener("pointerdown", event => this.beginSwipe(event));
        this.addEventListener("dragstart", event => event.preventDefault());
        this.show(0, false);
        this.start();
    }

    disconnectedCallback() {
        this.stop();
    }

    start() {
        this.stop();
        if (this.slides.length > 1 && !reducedMotion.matches) {
            this.timer = setInterval(() => this.show(this.index + 1, false), heroIntervalMs);
        }
    }

    stop() {
        clearInterval(this.timer);
    }

    beginSwipe(event) {
        if (event.pointerType === "mouse" && event.button !== 0) {
            return;
        }
        this.stop();
        const startX = event.clientX;
        const startY = event.clientY;
        const finish = end => {
            window.removeEventListener("pointerup", finish);
            window.removeEventListener("pointercancel", finish);
            const deltaX = end.clientX - startX;
            const deltaY = end.clientY - startY;
            if (end.type === "pointerup" && Math.abs(deltaX) >= swipeThresholdPx && Math.abs(deltaX) > Math.abs(deltaY)) {
                this.show(deltaX < 0 ? this.index + 1 : this.index - 1, false);
                suppressNextClick(this);
            }
            this.start();
        };
        window.addEventListener("pointerup", finish);
        window.addEventListener("pointercancel", finish);
    }

    show(index, restart) {
        this.index = (index + this.slides.length) % this.slides.length;
        this.slides.forEach((slide, position) => slide.toggleAttribute(activeAttribute, position === this.index));
        this.bullets.forEach((bullet, position) => bullet.setAttribute("aria-current", position === this.index ? "true" : "false"));
        if (restart) {
            this.start();
        }
    }
}

class UnaykaRail extends HTMLElement {
    connectedCallback() {
        this.hovered = false;
        this.pausedUntil = 0;
        this.addEventListener("pointerenter", () => (this.hovered = true));
        this.addEventListener("pointerleave", () => (this.hovered = false));
        this.addEventListener("pointerdown", event => this.beginDrag(event));
        this.addEventListener("touchstart", () => this.pause(), { passive: true });
        this.addEventListener("wheel", () => this.pause(), { passive: true });
        this.addEventListener("focusin", () => this.pause());
        this.addEventListener("dragstart", event => event.preventDefault());
        if (!reducedMotion.matches) {
            this.timer = setInterval(() => this.advance(), railIntervalMs);
        }
    }

    disconnectedCallback() {
        clearInterval(this.timer);
    }

    pause() {
        this.pausedUntil = Date.now() + railResumeDelayMs;
    }

    advance() {
        const overflow = this.scrollWidth - this.clientWidth;
        if (this.hovered || Date.now() < this.pausedUntil || overflow < 1 || !this.firstElementChild) {
            return;
        }
        const gap = parseFloat(getComputedStyle(this).columnGap) || 0;
        const step = this.firstElementChild.getBoundingClientRect().width + gap;
        const atEnd = this.scrollLeft >= overflow - 2;
        this.scrollTo({ left: atEnd ? 0 : this.scrollLeft + step, behavior: "smooth" });
    }

    beginDrag(event) {
        this.pause();
        if (event.pointerType !== "mouse" || event.button !== 0) {
            return;
        }
        const startX = event.clientX;
        const startScroll = this.scrollLeft;
        let moved = false;
        const move = current => {
            const delta = current.clientX - startX;
            if (!moved && Math.abs(delta) < dragThresholdPx) {
                return;
            }
            moved = true;
            this.setAttribute(draggingAttribute, "");
            this.scrollLeft = startScroll - delta;
        };
        const finish = () => {
            window.removeEventListener("pointermove", move);
            window.removeEventListener("pointerup", finish);
            this.removeAttribute(draggingAttribute);
            if (moved) {
                suppressNextClick(this);
            }
            this.pause();
        };
        window.addEventListener("pointermove", move);
        window.addEventListener("pointerup", finish);
    }
}

const fades = new Set();
let fadeTimer;

function advanceFades() {
    fades.forEach(fade => fade.advance());
}

function startFadeTimer() {
    if (!fadeTimer && fades.size > 0 && !reducedMotion.matches) {
        fadeTimer = setInterval(advanceFades, fadeIntervalMs);
    }
}

function stopFadeTimerWhenIdle() {
    if (fades.size === 0) {
        clearInterval(fadeTimer);
        fadeTimer = undefined;
    }
}

class UnaykaFade extends HTMLElement {
    connectedCallback() {
        this.images = [...this.querySelectorAll("[data-fade]")];
        this.index = 0;
        if (this.images.length > 1) {
            fades.add(this);
            startFadeTimer();
        }
    }

    disconnectedCallback() {
        fades.delete(this);
        stopFadeTimerWhenIdle();
    }

    advance() {
        this.index = (this.index + 1) % this.images.length;
        this.images.forEach((image, position) => image.toggleAttribute(activeAttribute, position === this.index));
    }
}

class UnaykaCollapse extends HTMLElement {
    connectedCallback() {
        this.query = window.matchMedia(desktopQuery);
        this.sync = () => this.querySelectorAll("details").forEach(details => (details.open = this.query.matches));
        this.query.addEventListener("change", this.sync);
        this.sync();
    }

    disconnectedCallback() {
        this.query.removeEventListener("change", this.sync);
    }
}

class UnaykaShare extends HTMLElement {
    connectedCallback() {
        this.details = this.querySelector("details");
        const summary = this.querySelector("summary");
        const copyButton = this.querySelector("[data-copy]");
        if (navigator.share) {
            summary.addEventListener("click", event => {
                event.preventDefault();
                this.shareNatively();
            });
        }
        copyButton?.addEventListener("click", () => this.copyLink(copyButton));
        this.closeOutside = event => {
            if (!this.contains(event.target)) {
                this.details.open = false;
            }
        };
        document.addEventListener("click", this.closeOutside);
    }

    disconnectedCallback() {
        document.removeEventListener("click", this.closeOutside);
    }

    async shareNatively() {
        try {
            await navigator.share({ title: this.dataset.title, url: this.dataset.url });
        } catch {
            this.details.open = false;
        }
    }

    async copyLink(button) {
        const label = button.querySelector("[data-copy-label]");
        const original = label.textContent;
        try {
            await navigator.clipboard.writeText(this.dataset.url);
            label.textContent = this.dataset.copiedLabel;
            setTimeout(() => (label.textContent = original), copiedResetMs);
        } catch {
            label.textContent = original;
        }
    }
}

define("unayka-carousel", UnaykaCarousel);
define("unayka-rail", UnaykaRail);
define("unayka-fade", UnaykaFade);
define("unayka-collapse", UnaykaCollapse);
define("unayka-share", UnaykaShare);
