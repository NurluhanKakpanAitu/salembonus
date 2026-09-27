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
export async function api<T>(path: string, init: RequestInit = {}, retry = true): Promise<T> {
  const { accessToken, storeId } = useAuth.getState()
  const headers = new Headers(init.headers)
  headers.set('Accept-Language', getLanguage())
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  if (storeId) headers.set('X-Store-Id', storeId)

  const res = await fetch(`/api${path}`, { ...init, headers })

  if (res.status === 401 && retry && accessToken) {
    if (await refreshSession()) return api<T>(path, init, false)
    clearSession()
  }
  if (!res.ok) throw await toError(res)
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}
