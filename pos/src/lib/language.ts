import { create } from 'zustand'

export type Language = 'ru' | 'kk'

const KEY = 'salempos.language'

function read(): Language {
  try {
    const v = localStorage.getItem(KEY)
    return v === 'kk' || v === 'ru' ? v : 'ru'
  } catch {
    return 'ru'
  }
}

/**
 * Интерфейс тілі. ТЗ бойынша әдепкісі орысша. Кіргенге дейін браузерде сақталады,
 * кіргеннен кейін қызметкердің баптауынан алынады (сервер сақтайды).
 */
export const useLanguageStore = create<{ lang: Language; set: (l: Language) => void }>((set) => ({
  lang: read(),
  set: (lang) => {
    try {
      localStorage.setItem(KEY, lang)
    } catch {
      /* жеке терезеде сақталмауы мүмкін — маңызды емес */
    }
    document.documentElement.lang = lang
    set({ lang })
  },
}))

export const getLanguage = () => useLanguageStore.getState().lang
