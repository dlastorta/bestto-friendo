import { useEffect, useRef, useState } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError } from './api'

type Row = { playerId: string; displayName: string; position: number; points: number }
export function Standings({ tournamentId, onAccessLost }: { tournamentId: string; onAccessLost: () => void }) {
  const [rows, setRows] = useState<Row[] | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  async function refresh() {
    setBusy(true); setError(''); setRows(null)
    try {
      const result = await tournamentRequest<Row[]>(`/${tournamentId}/standings`)
      if (mounted.current) setRows(result)
    } catch (cause) {
      if (mounted.current) {
        if (cause instanceof AuthenticationError || cause instanceof TournamentPermissionError) onAccessLost()
        else setError(cause instanceof Error ? cause.message : 'No se pudo consultar la tabla.')
      }
    } finally { if (mounted.current) setBusy(false) }
  }
  return <section aria-label="Tabla de posiciones">
    <h4>Tabla de posiciones</h4>
    <p>Solo por puntos. Los empates comparten puesto; los criterios de desempate todavía no se aplican.</p>
    <button disabled={busy} onClick={() => void refresh()}>{busy ? 'Consultando tabla…' : 'Consultar tabla'}</button>
    {error && <p role="alert">{error}</p>}
    {rows && <table><caption>Puntos según resultados vigentes. Recargá después de corregir resultados.</caption>
      <thead><tr><th scope="col">Puesto</th><th scope="col">Jugador</th><th scope="col">Puntos</th></tr></thead>
      <tbody>{rows.map(row => <tr key={row.playerId}><td>{row.position}</td><th scope="row">{row.displayName}</th><td>{row.points}</td></tr>)}</tbody>
    </table>}
  </section>
}
