// Applies the saved theme before the first paint so a dark or light choice never flashes the other one.
// An external file (not inline) because the Content-Security-Policy only allows scripts from this origin.
try {
  var saved = window.localStorage.getItem('dss.theme')
  if (saved === 'light' || saved === 'dark') {
    document.documentElement.setAttribute('data-theme', saved)
  }
} catch (error) {
  // Blocked storage: follow the system preference.
}
