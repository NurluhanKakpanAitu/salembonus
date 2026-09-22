import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface AuthTokens {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  isNewCustomer: boolean
  profileCompleted: boolean
}

interface AuthState {
  accessToken: string | null
  refreshToken: string | null
  profileCompleted: boolean
  pendingPhone: string | null
  setTokens: (t: AuthTokens) => void
  setProfileCompleted: (v: boolean) => void
  setPendingPhone: (phone: string | null) => void
  clear: () => void
}

export const useAuth = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      refreshToken: null,
      profileCompleted: true,
      pendingPhone: null,
      setTokens: (t) =>
        set({ accessToken: t.accessToken, refreshToken: t.refreshToken, profileCompleted: t.profileCompleted }),
      setProfileCompleted: (v) => set({ profileCompleted: v }),
      setPendingPhone: (phone) => set({ pendingPhone: phone }),
      clear: () => set({ accessToken: null, refreshToken: null, profileCompleted: true, pendingPhone: null }),
    }),
    { name: 'salembonus-auth' },
  ),
)

export const isLoggedIn = () => !!useAuth.getState().accessToken
