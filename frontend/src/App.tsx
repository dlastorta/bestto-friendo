import { useCallback, useEffect, useState } from 'react'
import { fetchHealth, pendingHealth, type HealthCheck } from './features/system/health'
import { GamesCatalog } from './features/games/GamesCatalog'

type ServiceCardProps = {
  label: string
  description: string
  check: HealthCheck
  code: string
}

function ServiceCard({ label, description, check, code }: ServiceCardProps) {
  const status = check.state === 'ok'
    ? 'Conectado'
    : check.state === 'pending'
      ? 'Comprobando'
      : 'Sin conexión'

  return (
    <article className="service-card">
      <div className="service-card-top">
        <div className="service-icon" aria-hidden="true">{code}</div>
        <span className={`status status--${check.state}`} role="status">
          <span className="status-dot" />{status}
        </span>
      </div>
      <h3>{label}</h3>
      <p>{description}</p>
      <div className="service-detail">{check.detail}</div>
    </article>
  )
}

function App() {
  const [api, setApi] = useState<HealthCheck>(pendingHealth)
  const [database, setDatabase] = useState<HealthCheck>(pendingHealth)
  const [refreshing, setRefreshing] = useState(false)

  const refresh = useCallback(async () => {
    setRefreshing(true)
    const [apiHealth, dbHealth] = await Promise.all([
      fetchHealth('/api/health/live'),
      fetchHealth('/api/health/ready'),
    ])
    setApi(apiHealth)
    setDatabase(dbHealth)
    setRefreshing(false)
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  return (
    <div className="app-shell">
      <header className="site-header">
        <div className="logo-mark" aria-hidden="true">T</div>
        <div className="brand">
          <span className="brand-name">Tournament Manager</span>
          <span className="brand-subtitle">Developer workspace</span>
        </div>
        <span className="milestone">SLICE 01</span>
      </header>

      <main className="main-content">
        <div className="eyebrow"><span className="eyebrow-line" /> CATÁLOGO MULTIJUEGO</div>
        <h1>Cada torneo empieza<br /><span>con su juego.</span></h1>
        <p className="lead">
          Registrá juegos, sus clanes o colores y cuántos pueden combinarse.
          Cada regla permanece dentro del dominio, sin acoplarse a la base de datos.
        </p>

        <div className="section-heading">
          <div>
            <h2>Estado de los servicios</h2>
            <p>Chequeos reales a través de la API local.</p>
          </div>
          <button type="button" onClick={() => void refresh()} disabled={refreshing}>
            <span aria-hidden="true">↻</span> {refreshing ? 'Actualizando…' : 'Actualizar'}
          </button>
        </div>

        <div className="service-grid">
          <ServiceCard
            label="ASP.NET Core API"
            description="Servicio HTTP y endpoints de sistema."
            check={api}
            code="API"
          />
          <ServiceCard
            label="PostgreSQL"
            description="Disponibilidad de la base de datos."
            check={database}
            code="DB"
          />
        </div>

        <GamesCatalog />

        <div className="next-step">
          <span className="next-tag">LO QUE SIGUE</span>
          <div>
            <h2>Slice 02 · Identidad del jugador</h2>
            <p>Autenticación y perfiles para una biblioteca personal de mazos.</p>
          </div>
          <span className="next-arrow" aria-hidden="true">→</span>
        </div>

        <p className="footer-note">Slice 01 · Reglas en Domain · Persistencia aislada en Infrastructure.</p>
      </main>
    </div>
  )
}

export default App
