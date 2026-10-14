// The fixtures page's strip of week chips scrolls sideways. This slides it so
// the current week's chip sits in the middle, without moving the page itself
// (scrollIntoView would also scroll the window).
window.fixturesPage = {
    revealCurrentChip: function () {
        const chip = document.querySelector('.fx-week-chip.is-current');
        if (!chip) return;
        const strip = chip.parentElement;
        strip.scrollLeft = chip.offsetLeft - (strip.clientWidth - chip.offsetWidth) / 2;
    }
};
