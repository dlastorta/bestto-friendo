import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import App from './App'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('Slice 00 · pantalla de estado', () => {
  it('muestra API y PostgreSQL al consultar ambos health checks', async () => {
    const fetchMock = vi.fn(async (input: string) => ({
      ok: true,
      status: 200,
      json: async () => input === '/api/games' ? [] : ({ status: input.includes('/ready') ? 'ready' : 'healthy' }),
    }))
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    expect(screen.getByText('ASP.NET Core API')).toBeInTheDocument()
    expect(screen.getByText('PostgreSQL')).toBeInTheDocument()
    await waitFor(() => {
      expect(screen.getByText('Base de datos conectada')).toBeInTheDocument()
      expect(screen.getByText('Servicio operativo')).toBeInTheDocument()
    })
    expect(fetchMock).toHaveBeenCalledWith('/api/health/live', expect.anything())
    expect(fetchMock).toHaveBeenCalledWith('/api/health/ready', expect.anything())
  })

  it('distingue readiness caída de liveness operativa', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: string) => ({
      ok: !input.includes('/ready'),
      status: input.includes('/ready') ? 503 : 200,
      json: async () => input === '/api/games' ? [] : ({ status: 'healthy' }),
    })))

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Servicio operativo')).toBeInTheDocument()
      expect(screen.getByText('Servicio no disponible')).toBeInTheDocument()
    })
  })
})
