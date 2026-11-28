import { useEffect, useState } from 'react'

type Tie = { code: string; value: number; numerator: string; denominator: string }
type Published = {
  name: string; gameName: string; startedAt: string; finishedAt: string; publishedAt: string
  participants: { playerId: string; displayName: string; deck: { name: string; listText: string } | null }[]
  standings: { playerId: string; displayName: string; position: number; points: number; tieBreakers: Tie[] }[]
  matches: { round: number; playerOneId: string; playerTwoId: string; outcome: string; kind: string; playerOnePoints: number; playerTwoPoints: number }[]
  byes: { round: number; playerId: string; points: number }[]
}
const tieName = (code: string) => code === 'OMWPercentage' ? 'OMW%' : code === 'BuchholzCut' ? 'Buchholz con descarte' : code
export function PublicTournament({ id }: { id: string }) {
  const [data, setData] = useState<Published | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(true)
  const [reload, setReload] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    setData(null); setError(''); setBusy(true)
    async function load() {
      try {
        const response = await fetch(`/api/public/tournaments/${encodeURIComponent(id)}`, { credentials: 'omit', cache: 'no-store', signal: controller.signal })
        if (!response.ok) throw new Error(response.status === 404 ? 'El torneo no está publicado o el enlace no es válido.' : 'No se pudo consultar el torneo. Intentá nuevamente.')
        const result = await response.json() as Published
        if (!controller.signal.aborted) setData(result)
      } catch (cause) { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'No se pudo consultar el torneo.') }
      finally { if (!controller.signal.aborted) setBusy(false) }
    }
    void load()
    return () => controller.abort()
  }, [id, reload])
  const name = (player: string) => data?.participants.find(p => p.playerId === player)?.displayName ?? 'Jugador'
  return <main className="app-shell" aria-label="Torneo público">
    <a href="/">Volver al inicio</a>
    <button disabled={busy} onClick={() => setReload(n => n + 1)}>Actualizar resultados publicados</button>
    {busy && <p role="status">Consultando torneo…</p>}
    {error && <p role="alert">{error}</p>}
    {data && <>
      <h1>{data.name}</h1><p>{data.gameName} · Torneo finalizado</p>
      <p>Inicio: {new Date(data.startedAt).toLocaleString()} · Cierre: {new Date(data.finishedAt).toLocaleString()}</p>
      <p>Publicado: {new Date(data.publishedAt).toLocaleString()}</p>
      <table><caption>Clasificación final</caption>
        <thead><tr><th scope="col">Puesto</th><th scope="col">Jugador</th><th scope="col">Puntos</th>{data.standings[0]?.tieBreakers.map(t => <th scope="col" key={t.code}>{tieName(t.code)}</th>)}</tr></thead>
        <tbody>{data.standings.map(r => <tr key={r.playerId}><td>{r.position}</td><th scope="row">{r.displayName}</th><td>{r.points}</td>{r.tieBreakers.map(t => <td key={t.code} title={`${t.numerator}/${t.denominator}`}>{t.code === 'OMWPercentage' ? `${(t.value * 100).toFixed(2)}%` : t.value}</td>)}</tr>)}</tbody>
      </table>
      <p>Los puestos usan puntos y desempates históricos exactos; los valores mostrados están redondeados.</p>
      <section aria-label="Resultados por ronda"><h2>Partidas y byes</h2>
        {data.matches.map((m, i) => <p key={i}>Ronda {m.round}: {name(m.playerOneId)} vs {name(m.playerTwoId)} · {m.outcome === 'Draw' ? 'Empate' : `Ganó ${name(m.outcome === 'PlayerOneWin' ? m.playerOneId : m.playerTwoId)}`} · {m.kind === 'Forfeit' ? 'Forfeit' : 'Jugada'} · {m.playerOnePoints} / {m.playerTwoPoints} puntos</p>)}
        {data.byes.map(b => <p key={b.round}>Ronda {b.round}: bye de {name(b.playerId)} · {b.points} puntos</p>)}
      </section>
      <section aria-label="Presentaciones compartidas"><h2>Mazos compartidos por sus jugadores</h2>
        {data.participants.filter(p => p.deck).map(p => <details key={p.playerId}><summary>{p.displayName} · {p.deck!.name}</summary><pre>{p.deck!.listText}</pre></details>)}
        {!data.participants.some(p => p.deck) && <p>No hay presentaciones autorizadas para consulta pública.</p>}
      </section>
    </>}
  </main>
}
