import { useEffect, useRef, useState } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError } from './api'

type Row = { playerId: string; displayName: string; position: number; points: number; formulaVersion?: number; tieBreakers?: { code: string; value: number; numerator: string; denominator: string }[] }
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
    <p>Primero puntos, luego los desempates congelados en su orden. Si persiste la igualdad, se comparte puesto.</p>
    <button disabled={busy} onClick={() => void refresh()}>{busy ? 'Consultando tabla…' : 'Consultar tabla'}</button>
    {error && <p role="alert">{error}</p>}
    {rows && <table><caption>Puntos según resultados vigentes. Recargá después de corregir resultados.</caption>
      <thead><tr><th scope="col">Puesto</th><th scope="col">Jugador</th><th scope="col">Puntos</th>{rows[0]?.tieBreakers?.map(t => <th key={t.code} scope="col">{t.code === 'OMWPercentage' ? 'OMW%' : t.code === 'BuchholzCut' ? 'Buchholz con descarte' : t.code}</th>)}</tr></thead>
      <tbody>{rows.map(row => <tr key={row.playerId}><td>{row.position}</td><th scope="row">{row.displayName}</th><td>{row.points}</td>{row.tieBreakers?.map(t => <td key={t.code} title={`Valor exacto: ${t.numerator}/${t.denominator}`}>{t.code === 'OMWPercentage' ? `${(t.value * 100).toFixed(2)}%` : t.value}</td>)}</tr>)}</tbody>
    </table>}
    {rows && <p>{rows[0]?.formulaVersion === 0 ? 'Torneo histórico: clasificación solo por puntos, sin aplicar nuevas fórmulas.' : 'Valores mostrados redondeados; puestos calculados con valores exactos. Forfeits y pendientes excluidos de los enfrentamientos de desempate.'}</p>}
  </section>
}
