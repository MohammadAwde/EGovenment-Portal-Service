// SmartEGov - DeepL Translation via server proxy with loading overlay

if (!sessionStorage.getItem('session_started')) {
    localStorage.removeItem('smartegov_lang');
    sessionStorage.setItem('session_started', 'true');
}

let currentLang = localStorage.getItem('smartegov_lang') || 'en';
let isTranslating = false;

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
}

function switchLanguage() {
    if (currentLang === 'en') {
        translatePage();
    } else {
        currentLang = 'en';
        localStorage.setItem('smartegov_lang', 'en');
        location.reload();
    }
}

window.addEventListener('load', () => {
    if (currentLang === 'ar') {
        applyFromCache();
    }
});