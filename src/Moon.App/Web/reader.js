// Lector de MOON: muestra el libro con foliate-js y habla con la ventana (C#) por mensajes.
//   C# → aquí: { type: 'open' | 'settings' | 'next' | 'prev' | 'goTo' | 'goToFraction', ... }
//   aquí → C#: { type: 'ready' | 'relocate' | 'key' | 'external-link' | 'error', ... }
import './foliate-js/view.js'

const host = window.chrome.webview
const post = message => host.postMessage(message)
const message = text => { document.getElementById('message').textContent = text }

// Colores de la página: deben coincidir con los de la ventana (Themes/Tokens.*.xaml de MOON).
const THEMES = {
    light: { bg: '#ffffff', fg: '#1a1a1a', link: '#1a56db', scheme: 'light' },
    sepia: { bg: '#f4ecd8', fg: '#3b2f1e', link: '#7a4b12', scheme: 'light' },
    dark: { bg: '#121212', fg: '#e3e3e3', link: '#8ab4f8', scheme: 'dark' },
}
const FONTS = {
    original: null,
    serif: "Georgia, 'Times New Roman', serif",
    sans: "'Segoe UI', Arial, sans-serif",
}
// gap = espacio a los lados (en % de la página); max = ancho máximo de cada columna de texto.
const MARGINS = {
    narrow: { gap: '4%', max: 1000 },
    normal: { gap: '7%', max: 760 },
    wide: { gap: '12%', max: 620 },
}

let view = null
let settings = { fontSize: 100, font: 'original', margins: 'normal', theme: 'light', paginated: true }

const bookCss = s => {
    const t = THEMES[s.theme] ?? THEMES.light
    const font = FONTS[s.font]
    return `
    @namespace epub "http://www.idpf.org/2007/ops";
    html {
        color-scheme: ${t.scheme};
        font-size: ${s.fontSize}% !important;
    }
    html, body {
        color: ${t.fg} !important;
        background: transparent !important;
    }
    /* Muchos libros fijan el negro: se usa el color del tema para que se lea en oscuro y sepia. */
    body *:not(a) {
        color: inherit !important;
        background-color: transparent !important;
    }
    a:link, a:visited { color: ${t.link} !important; }
    ${font ? `body, body * { font-family: ${font} !important; }` : ''}
    p, li, blockquote, dd {
        line-height: 1.5;
        hanging-punctuation: allow-end last;
        widows: 2;
    }
    [align="left"] { text-align: left; }
    [align="right"] { text-align: right; }
    [align="center"] { text-align: center; }
    [align="justify"] { text-align: justify; }
    pre { white-space: pre-wrap !important; }
    aside[epub|type~="endnote"],
    aside[epub|type~="footnote"],
    aside[epub|type~="note"],
    aside[epub|type~="rearnote"] {
        display: none;
    }`
}

const applySettings = () => {
    const t = THEMES[settings.theme] ?? THEMES.light
    document.documentElement.style.setProperty('--bg', t.bg)
    document.documentElement.style.setProperty('--fg', t.fg)
    document.documentElement.style.colorScheme = t.scheme
    if (!view?.renderer) return
    const m = MARGINS[settings.margins] ?? MARGINS.normal
    view.renderer.setAttribute('flow', settings.paginated ? 'paginated' : 'scrolled')
    view.renderer.setAttribute('gap', m.gap)
    view.renderer.setAttribute('max-inline-size', `${m.max}px`)
    view.renderer.setAttribute('margin', '40px')
    view.renderer.setStyles?.(bookCss(settings))
}

// El título/autor pueden venir como texto o como { es: '…', en: '…' }.
const text = x => !x ? '' : typeof x === 'string' ? x : x.name ? text(x.name) : Object.values(x)[0] ?? ''

// Índice en lista plana con nivel (para la lista de la ventana).
const flattenToc = (items, depth = 0, out = []) => {
    for (const item of items ?? []) {
        out.push({ label: (item.label ?? '').trim(), href: item.href ?? '', depth })
        flattenToc(item.subitems, depth + 1, out)
    }
    return out
}

// Teclas: pasar página aquí mismo; el resto (F11, Esc, Ctrl +/−…) lo decide la ventana.
const onKey = e => {
    if (!view) return
    const k = e.key
    if (k === 'ArrowLeft' || k === 'PageUp') { view.goLeft(); e.preventDefault() }
    else if (k === 'ArrowRight' || k === 'PageDown' || (k === ' ' && !e.shiftKey)) { view.goRight(); e.preventDefault() }
    else if (k === ' ' && e.shiftKey) { view.prev(); e.preventDefault() }
    else if (k === 'Home' && e.ctrlKey) { view.goToFraction(0); e.preventDefault() }
    else if (k === 'End' && e.ctrlKey) { view.goToFraction(1); e.preventDefault() }
    else if (k.startsWith('F') || k === 'Escape' || e.ctrlKey) {
        post({ type: 'key', key: k, ctrl: e.ctrlKey, shift: e.shiftKey })
        if (e.ctrlKey && ['+', '-', '=', '0'].includes(k)) e.preventDefault()
    }
}
document.addEventListener('keydown', onKey)

// La rueda del ratón también pasa páginas (en modo páginas).
let wheelLock = 0
const onWheel = e => {
    if (!view || !settings.paginated || e.ctrlKey) return
    const now = Date.now()
    if (now - wheelLock < 350) return
    wheelLock = now
    if (e.deltaY > 0) view.next(); else if (e.deltaY < 0) view.prev()
}
document.addEventListener('wheel', onWheel, { passive: true })

const open = async ({ url, name, location, settings: s }) => {
    settings = s ?? settings
    applySettings()
    message('Abriendo…')
    try {
        const response = await fetch(url)
        if (!response.ok) throw new Error(`No se pudo leer el archivo (${response.status})`)
        const file = new File([await response.blob()], name)

        view = document.createElement('foliate-view')
        document.body.append(view)
        await view.open(file)
        view.addEventListener('load', ({ detail: { doc } }) => {
            doc.addEventListener('keydown', onKey)
            doc.addEventListener('wheel', onWheel, { passive: true })
        })
        view.addEventListener('relocate', ({ detail }) => post({
            type: 'relocate',
            cfi: detail.cfi ?? null,
            fraction: detail.fraction ?? 0,
            chapter: detail.tocItem?.label?.trim() ?? '',
            href: detail.tocItem?.href ?? '',
        }))
        view.addEventListener('external-link', e => {
            e.preventDefault()
            post({ type: 'external-link', href: e.detail.href_ })
        })
        // Si una parte del libro está dañada, se salta en vez de detener la lectura.
        view.book.transformTarget?.addEventListener('data', ({ detail }) => {
            detail.data = Promise.resolve(detail.data).catch(() => '')
        })
        applySettings()
        message('')
        await view.init({ lastLocation: location ?? null })

        const { book } = view
        post({
            type: 'ready',
            title: text(book.metadata?.title),
            author: [book.metadata?.author ?? []].flat().map(text).filter(Boolean).join(', '),
            dir: book.dir ?? 'ltr',
            toc: flattenToc(book.toc),
        })
    } catch (error) {
        message('')
        post({ type: 'error', message: String(error?.message ?? error) })
    }
}

host.addEventListener('message', ({ data }) => {
    switch (data?.type) {
        case 'open': open(data); break
        case 'settings': settings = data.settings; applySettings(); break
        case 'next': view?.goRight(); break
        case 'prev': view?.goLeft(); break
        case 'goTo': view?.goTo(data.target); break
        case 'goToFraction': view?.goToFraction(data.value); break
    }
})

post({ type: 'loaded' })
