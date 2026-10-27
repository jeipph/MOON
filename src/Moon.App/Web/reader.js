// Lector de MOON: muestra el libro con foliate-js y habla con la ventana (C#) por mensajes.
//   C# → aquí: { type: 'open' | 'settings' | 'next' | 'prev' | 'goTo' | 'goToFraction'
//                | 'highlight' | 'remove-highlight' | 'clear-selection', ... }
//   aquí → C#: { type: 'ready' | 'relocate' | 'key' | 'external-link' | 'error'
//                | 'selection' | 'selection-cleared' | 'annotation-clicked', ... }
import './foliate-js/view.js'
import { Overlayer } from './foliate-js/overlayer.js'

// Colores de subrayado: deben coincidir con Brush.Highlight.* de Themes/Tokens.Layout.xaml.
const HIGHLIGHTS = {
    Yellow: '#ffd54f',
    Green: '#81c784',
    Blue: '#64b5f6',
    Pink: '#f48fb1',
    Purple: '#ba68c8',
}

// Subrayados del libro: CFI → color. Se vuelven a dibujar cada vez que se carga un capítulo.
const highlights = new Map()
let lastLocation = null

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

const open = async ({ url, name, location, settings: s, annotations }) => {
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
        view.addEventListener('load', ({ detail: { doc, index } }) => {
            doc.addEventListener('keydown', onKey)
            doc.addEventListener('wheel', onWheel, { passive: true })
            // Al soltar el ratón (o Mayús + flechas) se mira si hay texto seleccionado.
            const check = () => setTimeout(() => checkSelection(doc, index), 10)
            doc.addEventListener('pointerup', check)
            doc.addEventListener('keyup', e => { if (e.shiftKey) check() })
        })
        view.addEventListener('relocate', ({ detail }) => {
            lastLocation = detail
            post({
                type: 'relocate',
                cfi: detail.cfi ?? null,
                fraction: detail.fraction ?? 0,
                chapter: detail.tocItem?.label?.trim() ?? '',
                href: detail.tocItem?.href ?? '',
            })
        })
        // Subrayados: foliate pide dibujarlos cada vez que un capítulo aparece en pantalla.
        view.addEventListener('create-overlay', () => {
            for (const [value, color] of highlights) view.addAnnotation({ value, color })
        })
        view.addEventListener('draw-annotation', ({ detail: { draw, annotation } }) => {
            draw(Overlayer.highlight, { color: HIGHLIGHTS[annotation.color] ?? HIGHLIGHTS.Yellow })
        })
        view.addEventListener('show-annotation', ({ detail }) => post({ type: 'annotation-clicked', cfi: detail.value }))
        for (const h of annotations ?? []) highlights.set(h.cfi, h.color)
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

// Texto seleccionado → la ventana muestra los colores para subrayar justo debajo.
const checkSelection = (doc, index) => {
    const selection = doc.getSelection()
    const text = selection?.toString().trim() ?? ''
    if (!selection || selection.isCollapsed || !text) {
        post({ type: 'selection-cleared' })
        return
    }
    const range = selection.getRangeAt(0)
    const box = range.getBoundingClientRect()
    const frame = doc.defaultView.frameElement.getBoundingClientRect()
    post({
        type: 'selection',
        cfi: view.getCFI(index, range),
        text,
        // Posición en la página de MOON (para colocar el menú de colores).
        x: frame.left + box.left + box.width / 2,
        y: frame.top + box.bottom,
        top: frame.top + box.top,
        chapter: lastLocation?.tocItem?.label?.trim() ?? '',
        fraction: lastLocation?.fraction ?? 0,
    })
}

const clearSelection = () => {
    for (const { doc } of view?.renderer?.getContents?.() ?? []) doc?.getSelection()?.removeAllRanges()
}

host.addEventListener('message', ({ data }) => {
    switch (data?.type) {
        case 'open': open(data); break
        case 'settings': settings = data.settings; applySettings(); break
        case 'next': view?.goRight(); break
        case 'prev': view?.goLeft(); break
        case 'goTo': view?.goTo(data.target); break
        case 'goToFraction': view?.goToFraction(data.value); break
        case 'highlight':
            highlights.set(data.cfi, data.color)
            view?.addAnnotation({ value: data.cfi, color: data.color })
            clearSelection()
            break
        case 'remove-highlight':
            highlights.delete(data.cfi)
            view?.deleteAnnotation({ value: data.cfi })
            break
        case 'clear-selection': clearSelection(); break
    }
})

post({ type: 'loaded' })
