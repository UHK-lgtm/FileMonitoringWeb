import { useEffect, useState } from 'react'

function App() {
  const [status, setStatus] = useState('checking...')

  useEffect(() => {
    fetch('/api/health')
      .then((response) => response.json())
      .then((body: { status: string }) => setStatus(body.status))
      .catch(() => setStatus('unavailable'))
  }, [])

  return (
    <main>
      <h1>File Monitoring</h1>
      <p>API status: {status}</p>
    </main>
  )
}

export default App
