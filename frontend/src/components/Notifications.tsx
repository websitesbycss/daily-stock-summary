import { Toaster } from 'sonner'

/** Mounted once; every notice goes through notify.ts. */
export function Notifications() {
  return <Toaster position="bottom-right" theme="system" closeButton duration={5000} />
}
