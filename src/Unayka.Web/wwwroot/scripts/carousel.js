const heroIntervalMs = 5000;
const fadeStepMs = 1800;
const railIntervalMs = 2500;
const railResumeDelayMs = 3500;
const railSettleMs = 150;
const railBufferSets = 1;
const previousAttribute = "data-previous";
const swipeThresholdPx = 50;
const dragThresholdPx = 6;
const copiedResetMs = 2000;
const desktopQuery = "(min-width: 768px)";
const activeAttribute = "data-active";
const draggingAttribute = "data-dragging";
const cloneAttribute = "data-rail-clone";
const staticAttribute = "data-static";

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
        this.querySelector("[data-hero-previous]")?.addEventListener("click", () => this.show(this.index - 1, true));
        this.querySelector("[data-hero-next]")?.addEventListener("click", () => this.show(this.index + 1, true));
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
        const nextIndex = (index + this.slides.length) % this.slides.length;
        if (nextIndex !== this.index || !this.slides[nextIndex].hasAttribute(activeAttribute)) {
            crossfade(this.slides, nextIndex);
        }
        this.index = nextIndex;
        this.bullets.forEach((bullet, position) => bullet.setAttribute("aria-current", position === this.index ? "true" : "false"));
        if (restart) {
            this.start();
        }
    }
}

class UnaykaRail extends HTMLElement {
    connectedCallback() {
        setTimeout(() => this.initialize(), 0);
    }

    initialize() {
        this.track = this.querySelector("[data-rail-track]");
        if (!this.track || !this.isConnected) {
            return;
        }
        this.disconnectedCallback();
        this.originals =[...this.track.children].filter(item => !item.hasAttribute(cloneAttribute));
        this.hovered = false;
        this.pausedUntil = 0;
        this.listen();
        this.buildLoop();
        this.resizeObserver = new ResizeObserver(() => this.buildLoop());
        this.resizeObserver.observe(this.track);
        if (!reducedMotion.matches) {
            this.timer = setInterval(() => this.advance(), railIntervalMs);
        }
    }

    listen() {
        if (this.listening) {
            return;
        }
        this.listening = true;
        this.addEventListener("pointerenter", () => (this.hovered = true));
        this.addEventListener("pointerleave", () => (this.hovered = false));
        this.track.addEventListener("pointerdown", event => this.beginDrag(event));
        this.track.addEventListener("touchstart", () => this.pause(), { passive: true });
        this.track.addEventListener("wheel", () => this.pause(), { passive: true });
        this.track.addEventListener("scroll", () => this.scheduleRecenter(), { passive: true });
        this.track.addEventListener("dragstart", event => event.preventDefault());
        this.addEventListener("focusin", () => this.pause());
        this.querySelector("[data-rail-previous]")?.addEventListener("click", () => this.move(-1, true));
        this.querySelector("[data-rail-next]")?.addEventListener("click", () => this.move(1, true));
    }

    disconnectedCallback() {
        clearInterval(this.timer);
        clearTimeout(this.recenterTimer);
        this.resizeObserver?.disconnect();
    }

    fitsAll() {
        const step = this.step();
        const visibleCount = step > 0 ? Math.max(1, Math.floor((this.track.clientWidth + this.gap()) / step)) : 0;
        return this.originals.length < visibleCount;
    }

    buildLoop() {
        this.track.querySelectorAll(`[${cloneAttribute}]`).forEach(clone => clone.remove());
        this.setWidth = 0;
        const gap = this.gap();
        const setWidth = this.originals.reduce((total, item) => total + item.getBoundingClientRect().width + gap, 0);
        const isStatic = setWidth < 1 || this.fitsAll();
        this.toggleAttribute(staticAttribute, isStatic);
        if (isStatic) {
            this.track.scrollLeft = 0;
            return;
        }
        const sidesNeeded = Math.ceil(this.track.clientWidth / setWidth) + railBufferSets;
        const cloneSet = () => this.originals.map(item => {
            const clone = item.cloneNode(true);
            clone.setAttribute(cloneAttribute, "");
            clone.setAttribute("aria-hidden", "true");
            clone.querySelectorAll("a, button").forEach(element => element.setAttribute("tabindex", "-1"));
            return clone;
        });
        for (let copy = 0; copy < sidesNeeded; copy++) {
            this.track.prepend(...cloneSet());
            this.track.append(...cloneSet());
        }
        this.setWidth = setWidth;
        this.homeLeft = setWidth * sidesNeeded - Math.max(0, (this.track.clientWidth - setWidth) / 2);
        this.track.scrollLeft = this.homeLeft;
    }

    gap() {
        return parseFloat(getComputedStyle(this.track).columnGap) || 0;
    }

    step() {
        return this.originals[0].getBoundingClientRect().width + this.gap();
    }

    pause() {
        this.pausedUntil = Date.now() + railResumeDelayMs;
    }

    advance() {
        if (!this.setWidth || this.hovered || Date.now() < this.pausedUntil || document.hidden) {
            return;
        }
        this.move(1, false);
    }

    move(direction, manual) {
        if (manual) {
            this.pause();
        }
        this.track.scrollBy({ left: direction * this.step(), behavior: "smooth" });
    }

    scheduleRecenter() {
        clearTimeout(this.recenterTimer);
        this.recenterTimer = setTimeout(() => this.recenter(), railSettleMs);
    }

    recenter() {
        if (!this.setWidth || this.track.hasAttribute(draggingAttribute)) {
            return;
        }
        const offset = this.track.scrollLeft - this.homeLeft;
        const wrapped = ((offset % this.setWidth) + this.setWidth) % this.setWidth;
        if (Math.abs(wrapped - offset) > 1) {
            this.track.scrollLeft = this.homeLeft + wrapped;
        }
    }

    beginDrag(event) {
        this.pause();
        if (event.pointerType !== "mouse" || event.button !== 0) {
            return;
        }
        const startX = event.clientX;
        const startScroll = this.track.scrollLeft;
        let moved = false;
        const move = current => {
            const delta = current.clientX - startX;
            if (!moved && Math.abs(delta) < dragThresholdPx) {
                return;
            }
            moved = true;
            this.track.setAttribute(draggingAttribute, "");
            this.track.scrollLeft = startScroll - delta;
        };
        const finish = () => {
            window.removeEventListener("pointermove", move);
            window.removeEventListener("pointerup", finish);
            this.track.removeAttribute(draggingAttribute);
            if (moved) {
                suppressNextClick(this.track);
            }
            this.pause();
            this.scheduleRecenter();
        };
        window.addEventListener("pointermove", move);
        window.addEventListener("pointerup", finish);
    }
}

function crossfade(items, nextIndex) {
    const current = items.find(item => item.hasAttribute(activeAttribute));
    items.forEach(item => item.removeAttribute(previousAttribute));
    current?.setAttribute(previousAttribute, "");
    items.forEach((item, position) => item.toggleAttribute(activeAttribute, position === nextIndex));
}

const visibleFades = new Set();
let fadeTimer;
let lastFade;

function advanceNextVisibleFade() {
    if (document.hidden || visibleFades.size === 0) {
        return;
    }
    const queue = [...visibleFades];
    const next = queue[(queue.indexOf(lastFade) + 1) % queue.length];
    lastFade = next;
    next.advance();
}

const fadeVisibility = new IntersectionObserver(entries => entries.forEach(entry => {
    if (entry.isIntersecting) {
        visibleFades.add(entry.target);
    } else {
        visibleFades.delete(entry.target);
    }
}));

class UnaykaFade extends HTMLElement {
    connectedCallback() {
        this.images = [...this.querySelectorAll("[data-fade]")];
        this.index = 0;
        if (this.images.length < 2 || reducedMotion.matches) {
            return;
        }
        fadeVisibility.observe(this);
        fadeTimer ??= setInterval(advanceNextVisibleFade, fadeStepMs);
    }

    disconnectedCallback() {
        fadeVisibility.unobserve(this);
        visibleFades.delete(this);
    }

    advance() {
        this.index = (this.index + 1) % this.images.length;
        crossfade(this.images, this.index);
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

document.addEventListener("dragstart", event => {
    const target = event.target instanceof Element ? event.target : null;
    if (target?.closest("img, a, picture")) {
        event.preventDefault();
    }
});

document.addEventListener("click", event => {
    const link = event.target instanceof Element ? event.target.closest("a[href]") : null;
    if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || (link.target && link.target !== "_self")) {
        return;
    }

    const url = new URL(link.href, location.href);
    if (url.origin !== location.origin || url.pathname !== location.pathname || url.search !== location.search) {
        return;
    }

    const anchor = url.hash ? document.getElementById(decodeURIComponent(url.hash.slice(1))) : null;
    if (url.hash && !anchor) {
        return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    const behavior = matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth";
    if (anchor) {
        anchor.scrollIntoView({ behavior, block: "start" });
        history.pushState(history.state, "", url.hash);
        return;
    }

    window.scrollTo({ top: 0, behavior });
}, true);
