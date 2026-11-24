export type HealthState = 'pending' | 'ok' | 'error'

export type HealthCheck = {
  state: HealthState
  detail: string
}

type HealthResponse = { status?: string }

export const pendingHealth: HealthCheck = {
  state: 'pending',
  detail: 'Comprobando conexión…',
}

export async function fetchHealth(path: string): Promise<HealthCheck> {
  try {
    const response = await fetch(path, {
      headers: { Accept: 'application/json' },
    })

    if (!response.ok) {
      return {
        state: 'error',
        detail: response.status === 503
          ? 'Servicio no disponible'
          : `Error HTTP ${response.status}`,
      }
    }

    const payload = (await response.json()) as HealthResponse
    return {
      state: 'ok',
      detail: payload.status === 'ready' ? 'Base de datos conectada' : 'Servicio operativo',
    }
  } catch {
    return {
      state: 'error',
      detail: 'No se pudo conectar al servidor',
    }
  }
}
