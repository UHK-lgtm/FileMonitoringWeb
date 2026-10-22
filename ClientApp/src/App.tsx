import { useEffect, useState } from 'react'

type MonitoringDefinition = {
  id: number
  name: string
  relativePath: string
  fileNamePattern: string
  isEnabled: boolean
}

type LoadState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; definitions: MonitoringDefinition[] }

function App() {
  const [state, setState] = useState<LoadState>({ status: 'loading' })

  useEffect(() => {
    fetch('/api/monitoring-definitions')
      .then((response) => {
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`)
        }
        return response.json() as Promise<MonitoringDefinition[]>
      })
      .then((definitions) => setState({ status: 'ready', definitions }))
      .catch(() => setState({ status: 'error' }))
  }, [])

  return (
    <main>
      <h1>File Monitoring</h1>
      <h2>Monitoring Definitions</h2>

      {state.status === 'loading' && <p>Loading...</p>}
      {state.status === 'error' && <p>Could not load monitoring definitions.</p>}

      {state.status === 'ready' && (
        <table>
          <thead>
            <tr>
              <th>Id</th>
              <th>Name</th>
              <th>Relative Path</th>
              <th>File Pattern</th>
              <th>Enabled</th>
            </tr>
          </thead>
          <tbody>
            {state.definitions.map((definition) => (
              <tr key={definition.id}>
                <td>{definition.id}</td>
                <td>{definition.name}</td>
                <td>{definition.relativePath}</td>
                <td>{definition.fileNamePattern}</td>
                <td>{definition.isEnabled ? 'Yes' : 'No'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </main>
  )
}

export default App
