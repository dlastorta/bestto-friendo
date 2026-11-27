import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { TournamentLifecycle } from './TournamentLifecycle'

const tournament = { id: 'cup', name: 'Copa', gameId: 'game', status: 'InProgress', rulesVersion: 1, rules: null, matchesVersion: 2, resultsVersion: 1, lifecycleVersion: 0 }
const response = (body: unknown, status = 200) => ({ ok: status < 400, status, json: async () => body })
afterEach(() => vi.unstubAllGlobals())

it('revisa versiones y exige confirmación antes de cerrar con CSRF', async () => {
  const saved = vi.fn()
  const request = vi.fn(async (url: string, init?: RequestInit) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    return response(init?.method === 'POST' ? { ...tournament, status: 'Finished' } : tournament)
  })
  vi.stubGlobal('fetch', request)
  render(<TournamentLifecycle tournament={tournament} onSaved={saved} onAccessLost={vi.fn()} />)
  expect(screen.queryByRole('button', { name: 'Confirmar finalización' })).not.toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Revisar cierre' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Confirmar finalización' }))
  await screen.findByRole('button', { name: 'Revisar cierre' })
  expect(request).toHaveBeenCalledWith('/api/tournaments/cup/finish', expect.objectContaining({
    body: JSON.stringify({ expectedLifecycleVersion: 0, expectedMatchesVersion: 2, expectedResultsVersion: 1 }),
    headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
  }))
})

it('exige motivo para reabrir y comunica que los cruces permanecen fijos', async () => {
  const saved = vi.fn()
  const request = vi.fn(async (url: string) => response(url === '/api/auth/csrf' ? { token: 'csrf' } : { ...tournament, status: 'Correction' }))
  vi.stubGlobal('fetch', request)
  render(<TournamentLifecycle tournament={{ ...tournament, status: 'Finished', lifecycleVersion: 1 }} onSaved={saved} onAccessLost={vi.fn()} />)
  expect(screen.getByRole('button', { name: 'Reabrir para corregir' })).toBeDisabled()
  expect(screen.getByText(/No permite generar ni reemplazar rondas/)).toBeInTheDocument()
  fireEvent.change(screen.getByLabelText('Motivo de reapertura'), { target: { value: 'Error de carga' } })
  fireEvent.click(screen.getByRole('button', { name: 'Reabrir para corregir' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalledOnce())
  expect(request).toHaveBeenCalledWith('/api/tournaments/cup/reopen', expect.objectContaining({ body: JSON.stringify({ expectedLifecycleVersion: 1, reason: 'Error de carga' }) }))
})
