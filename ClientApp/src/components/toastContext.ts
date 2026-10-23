import { createContext, useContext } from 'react'

export type Notify = (text: string, tone?: 'success' | 'error') => void

/// The notify function provided by ToastProvider. Outside a provider it does nothing.
export const ToastContext = createContext<Notify>(() => undefined)

export function useToast(): Notify {
  return useContext(ToastContext)
}
