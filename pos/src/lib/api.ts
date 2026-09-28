import { getLanguage } from './language'
import { clearSession, refreshSession, useAuth } from './auth'

/** Сервер қатесі. field берілсе — қате сол өрістің астында көрсетіледі. */
export class ApiError extends Error {
  status: number
  field: string | null
  constructor(status: number, message: string, field: string | null = null) {
    super(message)
    this.status = status
    this.field = field
  }
}

async function toError(res: Response): Promise<ApiError> {
  try {
    const body = await res.json()
    return new ApiError(res.status, body?.detail ?? body?.title ?? res.statusText, body?.field ?? null)
  } catch {
    return new ApiError(res.status, res.statusText)
  }
}

/**
 * Барлық API сұранысы осы арқылы: тіл, токен және белсенді дүкен (X-Store-Id) қосылады.
 * Токен ескірсе — бір рет жаңартып, сұранысты қайталайды.
 */
async function send(path: string, init: RequestInit, retry: boolean): Promise<Response> {
  const { accessToken, storeId } = useAuth.getState()
  const headers = new Headers(init.headers)
  headers.set('Accept-Language', getLanguage())
  // FormData-ға Content-Type қоймаймыз: браузер оны boundary-мен өзі қояды.
  if (typeof init.body === 'string' && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  if (storeId) headers.set('X-Store-Id', storeId)

  const res = await fetch(`/api${path}`, { ...init, headers })

  if (res.status === 401 && retry && accessToken) {
    if (await refreshSession()) return send(path, init, false)
    clearSession()
  }
  if (!res.ok) throw await toError(res)
  return res
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await send(path, init, true)
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

/** Файлды жүктеп алу (мысалы, Excel экспорт): аты сервердің Content-Disposition-ынан. */
export async function download(path: string, fallbackName: string) {
  const res = await send(path, {}, true)
  const disposition = res.headers.get('Content-Disposition') ?? ''
  const name = /filename\*=UTF-8''([^;]+)/.exec(disposition)?.[1] ?? /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? fallbackName
  const url = URL.createObjectURL(await res.blob())
  const a = document.createElement('a')
  a.href = url
  a.download = decodeURIComponent(name)
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
