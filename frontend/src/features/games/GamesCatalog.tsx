import { useEffect, useState, type FormEvent } from 'react'
import { createGame, listGames, type Game } from './api'

export function GamesCatalog() {
  const [games, setGames] = useState<Game[]>([])
  const [loading, setLoading] = useState(true)
  const [loadingError, setLoadingError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [factionsText, setFactionsText] = useState('')
  const [minimumFactions, setMinimumFactions] = useState(1)
  const [maximumFactions, setMaximumFactions] = useState(1)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const fetchCatalog = async () => {
    setLoading(true)
    setLoadingError(null)
    try {
      setGames(await listGames())
    } catch (error) {
      setLoadingError(error instanceof Error ? error.message : 'No se pudo cargar el catálogo.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void fetchCatalog()
  }, [])

  const submitGame = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSaveError(null)
    setMessage(null)
    const factions = factionsText.split(/[\r\n,]+/).map(value => value.trim()).filter(Boolean)

    if (factions.length === 0) {
      setSaveError('Agregá al menos una facción o color.')
      return
    }

    setSaving(true)
    try {
      const game = await createGame({ name: name.trim(), factions, minimumFactions, maximumFactions })
      setGames(previous => [...previous, game].sort((left, right) => left.name.localeCompare(right.name)))
      setName('')
      setFactionsText('')
      setMinimumFactions(1)
      setMaximumFactions(1)
      setMessage(`El juego «${game.name}» se creó correctamente.`)
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : 'No se pudo crear el juego.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="games-section" aria-labelledby="games-heading">
      <div className="section-heading">
        <div>
          <div className="eyebrow section-eyebrow"><span className="eyebrow-line" /> SLICE 01</div>
          <h2 id="games-heading">Juegos y facciones</h2>
          <p>Definí el universo de cada juego antes de crear torneos o mazos.</p>
        </div>
        <span className="catalog-count">{games.length} {games.length === 1 ? 'juego' : 'juegos'}</span>
      </div>

      <div className="catalog-layout">
        <form className="game-form" onSubmit={(event) => void submitGame(event)}>
          <div className="form-header">
            <span className="form-icon" aria-hidden="true">+</span>
            <div>
              <h3>Nuevo juego</h3>
              <p>Creá un juego con sus facciones y reglas de selección.</p>
            </div>
          </div>

          <label htmlFor="game-name">Nombre del juego</label>
          <input
            id="game-name"
            required
            maxLength={120}
            minLength={2}
            value={name}
            onChange={event => setName(event.target.value)}
            placeholder="Ej. Legend of the Five Rings"
          />

          <label htmlFor="game-factions">Facciones o colores</label>
          <textarea
            id="game-factions"
            required
            value={factionsText}
            onChange={event => setFactionsText(event.target.value)}
            placeholder={'León\nGrulla\nDragón\nEscorpión'}
            rows={5}
            aria-describedby="factions-help"
          />
          <p className="input-help" id="factions-help">Una por línea, o separadas por coma. Máximo 50.</p>

          <div className="form-columns">
            <div>
              <label htmlFor="minimum-factions">Selección mínima</label>
              <input
                id="minimum-factions"
                type="number"
                required
                min={1}
                max={50}
                value={minimumFactions}
                onChange={event => setMinimumFactions(Number(event.target.value))}
              />
            </div>
            <div>
              <label htmlFor="maximum-factions">Selección máxima</label>
              <input
                id="maximum-factions"
                type="number"
                required
                min={1}
                max={50}
                value={maximumFactions}
                onChange={event => setMaximumFactions(Number(event.target.value))}
              />
            </div>
          </div>
          <p className="input-help">Ej.: L5R puede usar 1; Magic puede permitir hasta 5 colores.</p>
          {saveError && <div role="alert" className="form-alert form-alert--error">{saveError}</div>}
          {message && <div role="status" className="form-alert form-alert--success">{message}</div>}
          <button type="submit" className="primary-button" disabled={saving}>
            {saving ? 'Guardando…' : 'Crear juego'}
          </button>
        </form>

        <div className="catalog-list" aria-live="polite">
          {loading && <div className="catalog-empty">Cargando juegos…</div>}
          {loadingError && (
            <div className="catalog-empty" role="alert">
              <p>{loadingError}</p>
              <button type="button" onClick={() => void fetchCatalog()}>Reintentar</button>
            </div>
          )}
          {!loading && !loadingError && games.length === 0 && (
            <div className="catalog-empty">
              <span className="empty-symbol" aria-hidden="true">◇</span>
              <h3>Todavía no hay juegos</h3>
              <p>Creá el primero para comenzar a configurar facciones.</p>
            </div>
          )}
          {!loading && !loadingError && games.map(game => (
            <article key={game.id} className="game-card">
              <div className="game-card-heading">
                <div className="game-monogram" aria-hidden="true">{game.name[0].toUpperCase()}</div>
                <div>
                  <h3>{game.name}</h3>
                  <p>{game.factions.length} {game.factions.length === 1 ? 'facción definida' : 'facciones definidas'}</p>
                </div>
              </div>
              <div className="faction-tags">
                {game.factions.map(faction => <span key={faction.id} className="faction-tag">{faction.name}</span>)}
              </div>
              <p className="selection-rule">Selección permitida: <strong>
                {game.minimumFactions === game.maximumFactions
                  ? `${game.minimumFactions} ${game.minimumFactions === 1 ? 'facción' : 'facciones'}`
                  : `${game.minimumFactions} a ${game.maximumFactions} facciones`}
              </strong></p>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}
