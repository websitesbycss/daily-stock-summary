import { useEffect, useReducer, useRef, useState } from 'react'
import { notify } from '../notify'
import { dashboardReducer } from './dashboardReducer'
import { readDashboard, saveDashboard } from './storage'

/** Dashboard state, restored from and saved to localStorage; problems with either are reported once. */
export function useDashboard() {
  const [initial] = useState(() => readDashboard())
  const [state, dispatch] = useReducer(dashboardReducer, initial.state)
  const reportedSaveFailure = useRef(false)

  useEffect(() => {
    if (initial.restoreFailed) {
      notify.info(
        'Your saved dashboard could not be restored, so you are starting fresh.',
        'restore',
      )
    }
  }, [initial])

  useEffect(() => {
    if (!saveDashboard(state) && !reportedSaveFailure.current) {
      reportedSaveFailure.current = true
      notify.error('Your layout could not be saved in this browser.', 'save')
    }
  }, [state])

  return { state, dispatch }
}
