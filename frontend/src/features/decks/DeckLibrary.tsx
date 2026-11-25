import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { listGames, type Game } from '../games/api'
import { AuthenticationError } from '../identity/api'
import { deckRequest, type Deck, type DeckRevision, type DeckSummary } from './api'

export function DeckLibrary({ onSessionExpired }: { onSessionExpired: () => void }) {
  const [games, setGames] = useState<Game[]>([])
  const [decks, setDecks] = useState<DeckSummary[]>([])
  const [filter, setFilter] = useState('')
  const [archived, setArchived] = useState(false)
  const [selected, setSelected] = useState<Deck | null>(null)
  const [history, setHistory] = useState<DeckRevision[]>([])
  const [gameId, setGameId] = useState('')
  const [name, setName] = useState('')
  const [listText, setListText] = useState('')
  const [factions, setFactions] = useState<string[]>([])
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)
  const mounted = useRef(false)
  const game = games.find(item => item.id === gameId)

  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])

  const fail = useCallback((cause: unknown) => {
    if (!mounted.current) return
    if (cause instanceof AuthenticationError) { onSessionExpired(); return }
    setError(cause instanceof Error ? cause.message : 'No se pudo cargar la biblioteca.')
  }, [onSessionExpired])

  useEffect(() => {
    let active = true
    listGames().then(result => { if (active) setGames(result) }).catch(cause => { if (active) fail(cause) })
    return () => { active = false }
  }, [fail, reload])

  useEffect(() => {
    let active = true
    setLoading(true)
    const query = new URLSearchParams({ archived: String(archived) })
    if (filter) query.set('gameId', filter)
    deckRequest<DeckSummary[]>(`?${query}`).then(result => {
      if (active) setDecks(result)
    }).catch(cause => { if (active) fail(cause) }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [filter, archived, reload, fail])

  function reset() {
    setSelected(null); setHistory([]); setName(''); setListText(''); setFactions([]); setGameId(''); setError('')
  }

  function show(deck: Deck, revisions: DeckRevision[]) {
    setSelected(deck); setHistory(revisions); setGameId(deck.gameId); setName(deck.current.name)
    setListText(deck.current.listText); setFactions(deck.current.factionIds)
  }

  async function open(id: string) {
    setBusy(true); setError('')
    try {
      const [deck, revisions] = await Promise.all([
        deckRequest<Deck>(`/${id}`), deckRequest<DeckRevision[]>(`/${id}/revisions`),
      ])
      if (mounted.current) show(deck, revisions)
    } catch (cause) { fail(cause) }
    finally { if (mounted.current) setBusy(false) }
  }

  async function save(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      const deck = await deckRequest<Deck>(selected ? `/${selected.id}` : '', selected ? 'PUT' : 'POST', {
        gameId, name, listText, factionIds: factions, ...(selected ? { expectedVersion: selected.version } : {}),
      })
      if (mounted.current) {
        show(deck, [deck.current, ...history]); setReload(value => value + 1)
      }
    } catch (cause) { fail(cause) }
    finally { if (mounted.current) setBusy(false) }
  }

  async function archive() {
    if (!selected) return
    setBusy(true); setError('')
    try {
      const deck = await deckRequest<Deck>(`/${selected.id}/archive`, 'POST', { expectedVersion: selected.version })
      if (mounted.current) { show(deck, history); setReload(value => value + 1) }
    } catch (cause) { fail(cause) }
    finally { if (mounted.current) setBusy(false) }
  }

  return <section className="deck-library" aria-labelledby="deck-heading">
    <div className="section-heading"><div><h2 id="deck-heading">Mi biblioteca de mazos</h2>
      <p>Solo vos podés ver y gestionar estos mazos.</p></div>
      <button type="button" disabled={busy} onClick={reset}>Nuevo mazo</button></div>
    <div className="deck-filters">
      <label>Filtrar por juego<select value={filter} disabled={busy} onChange={event => setFilter(event.target.value)}>
        <option value="">Todos los juegos</option>{games.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
      </select></label>
      <label className="check-label"><input type="checkbox" checked={archived} disabled={busy} onChange={event => setArchived(event.target.checked)} />Ver archivados</label>
      <button type="button" disabled={busy || loading} onClick={() => setReload(value => value + 1)}>Recargar biblioteca</button>
    </div>
    {error && <p role="alert">{error}</p>}
    <div className="deck-layout">
      <div>{loading ? <p role="status">Cargando mazos…</p> : decks.length === 0 ? <p>No hay mazos {archived ? 'archivados' : 'activos'} para este filtro.</p> :
        <ul className="deck-list">{decks.map(deck => <li key={deck.id}>
          <button type="button" disabled={busy} onClick={() => void open(deck.id)}>{deck.name} · revisión {deck.revision}</button>
          <small>{games.find(item => item.id === deck.gameId)?.name}</small>
        </li>)}</ul>}
      </div>
      <div className="deck-editor">
        <h3>{selected ? (selected.isArchived ? 'Mazo archivado' : 'Editar mazo') : 'Crear mazo'}</h3>
        <form onSubmit={event => void save(event)}>
          <fieldset disabled={busy || selected?.isArchived}>
            <label>Nombre del mazo<input value={name} onChange={event => setName(event.target.value)} required /></label>
            <label>Juego del mazo<select value={gameId} disabled={selected !== null} required onChange={event => { setGameId(event.target.value); setFactions([]) }}>
              <option value="">Elegí un juego</option>{games.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select></label>
            {game && <fieldset><legend>Facciones · entre {game.minimumFactions} y {game.maximumFactions}</legend>
              {game.factions.map(faction => <label className="check-label" key={faction.id}>
                <input type="checkbox" checked={factions.includes(faction.id)} onChange={event => setFactions(current => event.target.checked ? [...current, faction.id] : current.filter(id => id !== faction.id))} />{faction.name}
              </label>)}
            </fieldset>}
            <label>Lista de cartas<textarea rows={10} value={listText} onChange={event => setListText(event.target.value)} placeholder={'4 x Nombre de la carta\n2 x Otra carta'} /></label>
            <p>Texto libre. Podés usar «N x Nombre de la carta»; se guarda tal como lo escribís, sin controles de cartas ni cantidades.</p>
            {!selected?.isArchived && <button type="submit">{busy ? 'Guardando…' : selected ? 'Guardar revisión' : 'Crear mazo'}</button>}
          </fieldset>
        </form>
        {selected && <div className="deck-actions">
          {!selected.isArchived && <button type="button" disabled={busy} onClick={() => void archive()}>Archivar mazo</button>}
          <button type="button" disabled={busy} onClick={() => void open(selected.id)}>Recargar mazo (descarta cambios)</button>
        </div>}
        {history.length > 0 && <div><h3>Historial de revisiones</h3>{history.map(revision => <details key={revision.number}>
          <summary>Revisión {revision.number} · {revision.name}</summary>
          <p>{revision.factionIds.map(id => game?.factions.find(f => f.id === id)?.name ?? id).join(', ')}</p>
          <pre className="deck-text">{revision.listText || '(Lista vacía)'}</pre>
        </details>)}</div>}
      </div>
    </div>
  </section>
}
