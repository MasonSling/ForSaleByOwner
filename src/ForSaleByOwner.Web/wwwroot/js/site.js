document.addEventListener('alpine:init', () => {
    Alpine.data('journeyGuide', () => ({ open: false, get guideClass() { return this.open ? 'guide-open' : ''; }, openGuide() { this.open = true; requestAnimationFrame(() => document.querySelector('.guide-close')?.focus()); }, closeGuide() { this.open = false; document.querySelector('.guide-mobile-button')?.focus(); } }));
    Alpine.data('documentPreview', () => ({
        name: '', url: null, error: '', isImage: false,
        get hasFile() { return !!this.url; }, get imageUrl() { return this.isImage ? this.url : null; },
        choose(event) {
            if(this.url) URL.revokeObjectURL(this.url);
            this.url = null; this.name = ''; this.error = ''; this.isImage = false;
            const file = event.target.files[0]; if(!file) return;
            if(!['application/pdf','image/jpeg','image/png','image/webp'].includes(file.type) || file.size > 10*1024*1024) { this.error = 'Choose a PDF, JPG, PNG, or WebP under 10 MB.'; return; }
            this.name = file.name; this.isImage = file.type.startsWith('image/'); this.url = URL.createObjectURL(file);
        },
        destroy() { if(this.url) URL.revokeObjectURL(this.url); }
    }));
    Alpine.data('navigation', () => ({ open: false, toggle() { this.open = !this.open; }, get menuClass() { return this.open ? 'is-open' : ''; } }));
    Alpine.data('photos', () => ({ previews: [], error: '', preview(event) { this.previews.forEach(p => URL.revokeObjectURL(p.url)); this.error = ''; const files = Array.from(event.target.files); if(files.some(f => !['image/jpeg','image/png','image/webp'].includes(f.type) || f.size > 10 * 1024 * 1024)) { this.error = 'Choose JPG, PNG, or WebP images smaller than 10 MB.'; this.previews = []; return; } this.previews = files.slice(0,6).map(f => ({name:f.name,url:URL.createObjectURL(f)})); }, destroy() { this.previews.forEach(p => URL.revokeObjectURL(p.url)); } }));
});
document.addEventListener('click', event => {
    const button = event.target.closest('[data-clear-field]');
    if(!button) return;
    const input = document.getElementById(button.dataset.clearField);
    if(input) { input.value = ''; input.placeholder = "Unknown — saved when you continue"; input.focus(); input.dispatchEvent(new Event('input',{bubbles:true})); }
});
document.addEventListener('keydown', event => { if(event.key === 'Escape' && document.querySelector('.guide-open')) document.querySelector('.guide-close')?.click(); });
document.addEventListener('htmx:responseError', () => { document.getElementById('request-error').hidden = false; });
document.addEventListener('htmx:sendError', () => { document.getElementById('request-error').hidden = false; });
document.addEventListener('htmx:beforeRequest', () => { document.getElementById('request-error').hidden = true; });
