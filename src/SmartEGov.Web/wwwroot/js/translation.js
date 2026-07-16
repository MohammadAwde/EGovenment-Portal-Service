// SmartEGov - DeepL Translation via server proxy with loading overlay

if (!sessionStorage.getItem('session_started')) {
    localStorage.removeItem('smartegov_lang');
    sessionStorage.setItem('session_started', 'true');
}

let currentLang = localStorage.getItem('smartegov_lang') || 'en';
let isTranslating = false;

<<<<<<< HEAD
function hideOverlay() {
    const overlay = document.getElementById('translationOverlay');
    if (overlay) overlay.style.display = 'none';
}

function getCache() {
    try { return JSON.parse(sessionStorage.getItem('translation_cache') || '{}'); }
    catch { return {}; }
}

function saveCache(cache) {
    try { sessionStorage.setItem('translation_cache', JSON.stringify(cache)); }
    catch { }
}

async function translateBatch(texts) {
    const unique = [...new Set(texts.filter(t => t && t.trim().length > 1))];
    if (!unique.length) return {};
    try {
        const res = await fetch('/Translation/Translate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(unique)
        });
        if (!res.ok) return {};
        const data = await res.json();
        const result = {};
        if (data && data.translations) {
            unique.forEach((text, i) => {
                if (data.translations[i] && data.translations[i].text)
                    result[text] = data.translations[i].text;
            });
        }
        return result;
    } catch (e) {
        console.error('Translation failed:', e);
        return {};
    }
}

function getTranslatableNodes() {
    const nodes = [];
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
        acceptNode(node) {
            const parent = node.parentElement;
            if (!parent) return NodeFilter.FILTER_REJECT;
            const tag = parent.tagName;
            if (['SCRIPT', 'STYLE', 'INPUT', 'TEXTAREA', 'SELECT', 'CODE'].includes(tag))
                return NodeFilter.FILTER_REJECT;
            const text = node.textContent.trim();
            if (!text || text.length < 2) return NodeFilter.FILTER_REJECT;
            if (/^[\d\s\.,\-\+\(\)\/\:]+$/.test(text)) return NodeFilter.FILTER_REJECT;
            if (/^APT-|^REF-|^SR-|@|http|localhost|SmartEGov/.test(text)) return NodeFilter.FILTER_REJECT;
            if (/[\u0600-\u06FF]/.test(text)) return NodeFilter.FILTER_REJECT;
            return NodeFilter.FILTER_ACCEPT;
        }
    });
    let node;
    while (node = walker.nextNode()) nodes.push(node);
    return nodes;
}

function applyRTL() {
    document.documentElement.dir = 'rtl';
    document.documentElement.lang = 'ar';
    document.documentElement.style.fontFamily = "'Cairo', 'Tajawal', Tahoma, sans-serif";
    if (!document.getElementById('bootstrap-rtl')) {
        const link = document.createElement('link');
        link.id = 'bootstrap-rtl';
        link.rel = 'stylesheet';
        link.href = 'https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.rtl.min.css';
        document.head.appendChild(link);
    }
}

async function translatePage() {
    if (isTranslating) return;
    isTranslating = true;

    const btn = document.getElementById('langToggleBtn');
    if (btn) btn.innerHTML = '⏳ جاري الترجمة...';

    try {
        const nodes = getTranslatableNodes();
        const texts = nodes.map(n => n.textContent.trim());
        const cache = getCache();
        const uncached = [...new Set(texts.filter(t => !cache[t]))];

        if (uncached.length > 0) {
            const chunkSize = 50;
            for (let i = 0; i < uncached.length; i += chunkSize) {
                const chunk = uncached.slice(i, i + chunkSize);
                const translations = await translateBatch(chunk);
                Object.assign(cache, translations);
                const progress = Math.min(Math.round(((i + chunkSize) / uncached.length) * 100), 100);
                if (btn) btn.innerHTML = `⏳ ${progress}%`;
            }
            saveCache(cache);
        }

        nodes.forEach(node => {
            const original = node.textContent.trim();
            const translated = cache[original];
            if (translated && translated !== original)
                node.textContent = node.textContent.replace(original, translated);
        });

        applyRTL();
        if (btn) btn.innerHTML = '🌐 English';
        currentLang = 'ar';
        localStorage.setItem('smartegov_lang', 'ar');

    } catch (error) {
        console.error('Translation error:', error);
        if (btn) btn.innerHTML = '🌐 العربية';
    }

    hideOverlay();
    isTranslating = false;
}

async function applyFromCache() {
    applyRTL();
    const cache = getCache();
    const nodes = getTranslatableNodes();

    // Apply cached translations
    nodes.forEach(node => {
        const original = node.textContent.trim();
        const translated = cache[original];
        if (translated && translated !== original)
            node.textContent = node.textContent.replace(original, translated);
    });

    // Translate uncached text
    const uncached = [...new Set(nodes.map(n => n.textContent.trim()).filter(t => !cache[t] && t.length > 1))];
    if (uncached.length > 0) {
        const translations = await translateBatch(uncached);
        Object.assign(cache, translations);
        saveCache(cache);
        // Apply new translations
        nodes.forEach(node => {
            const original = node.textContent.trim();
            const translated = cache[original];
            if (translated && translated !== original)
                node.textContent = node.textContent.replace(original, translated);
        });
    }

    const btn = document.getElementById('langToggleBtn');
    if (btn) btn.innerHTML = '🌐 English';
    hideOverlay();
=======
// Build ordered keys (longer first) to prefer multi-word replacements
const AR_KEYS = Object.keys(AR).sort((a, b) => b.length - a.length);

function normalizeForCompare(s) {
    if (!s) return '';
    // normalize various dash characters to hyphen, collapse whitespace, lowercase
    return s.replace(/[\u2010\u2011\u2012\u2013\u2014\u2015\u2212\-]/g, '-').replace(/\s+/g, ' ').trim().toLowerCase();
}

// Precompute normalized keys map
const NORMALIZED_KEYS = {};
for (let k of AR_KEYS) NORMALIZED_KEYS[k] = normalizeForCompare(k);

function shouldSkipParent(parent) {
    if (!parent) return true;
    const tag = parent.tagName;
    // Never translate inside these tags or code blocks
    if (['SCRIPT', 'STYLE', 'INPUT', 'TEXTAREA', 'SELECT', 'PRE', 'CODE', 'KBD', 'SAMP', 'VAR', 'TT'].includes(tag)) return true;
    // Skip elements that are interactive controls handled by JS (bootstrap toggles etc.)
    if (parent.closest('[data-bs-toggle]') || parent.closest('[role="button"]') || parent.closest('a')) return false;
    return false;
}

function applyTranslations() {
    try {
        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                const parent = node.parentElement;
                if (!parent) return NodeFilter.FILTER_REJECT;
                if (shouldSkipParent(parent)) return NodeFilter.FILTER_REJECT;
                const text = node.textContent.trim();
                if (!text || text.length < 2) return NodeFilter.FILTER_REJECT;
                return NodeFilter.FILTER_ACCEPT;
            }
        });

        let node = null;
        while (node = walker.nextNode()) {
            const original = node.textContent;
            // Skip nodes that look like code fragments (contain braces or code comments)
            if (/[{}`<>]/.test(original) || original.indexOf('//') !== -1) continue;
            let translated = original;
            // Replace known phrases inside the text (case-sensitive map)
            for (let i = 0; i < AR_KEYS.length; i++) {
                const key = AR_KEYS[i];
                if (translated.indexOf(key) !== -1) {
                    // Replace all occurrences
                    const re = new RegExp(key.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&'), 'g');
                    translated = translated.replace(re, AR[key]);
                }
            }
            if (translated !== original) {
                node.textContent = translated;
            }
        }

        // Translate attributes (placeholders, alt text, titles)
        document.querySelectorAll('input[placeholder], textarea[placeholder]').forEach(function (el) {
            const ph = el.getAttribute('placeholder');
            if (ph && AR[ph]) el.setAttribute('placeholder', AR[ph]);
        });
        document.querySelectorAll('img[alt]').forEach(function (el) {
            const alt = el.getAttribute('alt');
            if (alt && AR[alt]) el.setAttribute('alt', AR[alt]);
        });
        document.querySelectorAll('[title]').forEach(function (el) {
            const t = el.getAttribute('title');
            if (t && AR[t]) el.setAttribute('title', AR[t]);
        });

        // Apply RTL and language attributes
        document.documentElement.setAttribute('dir', 'rtl');
        document.documentElement.setAttribute('lang', 'ar');
        document.documentElement.style.fontFamily = "'Cairo', 'Tajawal', Tahoma, sans-serif";

        // Bootstrap RTL: add once
        if (!document.getElementById('bootstrap-rtl')) {
            const link = document.createElement('link');
            link.id = 'bootstrap-rtl';
            link.rel = 'stylesheet';
            link.href = 'https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.rtl.min.css';
            document.head.appendChild(link);
        }

        // Update button label
        const btn = document.getElementById('langToggleBtn');
        if (btn) btn.innerHTML = '🌐 English';
    } catch (e) {
        console.error('applyTranslations error', e);
    }
}

// Remove stray code-like fragments rendered accidentally (e.g. 'else { }')
function removeStrayCodeFragments() {
    try {
        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                const txt = node.textContent || '';
                if (!txt.trim()) return NodeFilter.FILTER_REJECT;
                // match patterns like: else { }  or else {  or } alone on a line
                if (/^\s*else\s*\{\s*\}\s*$/i.test(txt) || /^\s*else\s*\{\s*$/i.test(txt) || /^\s*\}\s*$/i.test(txt))
                    return NodeFilter.FILTER_ACCEPT;
                return NodeFilter.FILTER_REJECT;
            }
        });

        let node;
        const toRemove = [];
        while (node = walker.nextNode()) {
            // prefer removing the whole block element if sensible
            const parent = node.parentElement;
            if (parent && parent.childElementCount === 0) {
                toRemove.push(parent);
            } else if (parent) {
                // remove the text node only
                toRemove.push(node);
            }
        }
        toRemove.forEach(n => { try { n.remove(); } catch(e){} });
    } catch (e) {
        // ignore
    }
>>>>>>> 4f13d81fa50e6f1ccc4d47850204d18b3ae4abd7
}

function switchLanguage() {
    if (currentLang === 'en') {
        translatePage();
    } else {
        // Revert to English: reload to restore server-rendered texts and remove RTL styles
        currentLang = 'en';
        localStorage.setItem('smartegov_lang', 'en');
        // Remove RTL stylesheet if present and restore attributes
        const rtlLink = document.getElementById('bootstrap-rtl');
        if (rtlLink && rtlLink.parentNode) rtlLink.parentNode.removeChild(rtlLink);
        document.documentElement.setAttribute('dir', 'ltr');
        document.documentElement.setAttribute('lang', 'en');
        document.documentElement.style.fontFamily = '';
        // Full reload ensures server-side resources and dynamic scripts are in sync
        location.reload();
    }
}

window.addEventListener('load', () => {
    if (currentLang === 'ar') {
        applyFromCache();
    }
    // Always remove stray code fragments that might be visible
    removeStrayCodeFragments();
});