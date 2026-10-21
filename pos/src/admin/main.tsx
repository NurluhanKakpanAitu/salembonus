import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ConfirmHost } from '../components/ui/Confirm'
import { Toaster } from '../components/ui/Toast'
import { useLanguageStore } from '../lib/language'
import { AdminApp } from './AdminApp'
import { ReasonHost } from './reason'
import '../index.css'

// Әкімшілік — ішкі құрал, интерфейсі орысша; ортақ компоненттер (растау, беттеу) де орысша сөйлесін.
if (useLanguageStore.getState().lang !== 'ru') useLanguageStore.getState().set('ru')
document.documentElement.lang = 'ru'

const queryClient = new QueryClient({ defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } } })

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AdminApp />
        <ConfirmHost />
        <Toaster />
        <ReasonHost />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
