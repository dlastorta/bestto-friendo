import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { PublicTournament } from './PublicTournament'
import { TournamentPublication } from './TournamentPublication'

const published = { name: 'Copa pública', gameName: 'Cartas', startedAt: '2026-11-27T10:00:00Z', finishedAt: '2026-11-27T11:00:00Z', publishedAt: '2026-11-27T12:00:00Z',
  participants: [{ playerId: 'a', displayName: 'Ana', deck: null }, { playerId: 'b', displayName: 'Bruno', deck: { name: 'Compartido', listText: '<script>alert(1)</script>' } }],
  standings: [{ playerId: 'a', displayName: 'Ana', position: 1, points: 3, tieBreakers: [] }],
  matches: [{ round: 1, playerOneId: 'a', playerTwoId: 'b', outcome: 'PlayerOneWin', kind: 'Played', playerOnePoints: 3, playerTwoPoints: 0 }], byes: [] }
const response = (body: unknown, status = 200) => ({ ok: status < 400, status, json: async () => body })
afterEach(() => vi.unstubAllGlobals())

it('muestra resultados sin sesión y únicamente presentaciones autorizadas como texto', async () => {
  const request = vi.fn(async () => response(published))
  vi.stubGlobal('fetch', request)
  const { container } = render(<PublicTournament id="cup" />)
  expect(await screen.findByRole('heading', { name: 'Copa pública' })).toBeInTheDocument()
  expect(screen.getByText(/Ganó Ana/)).toBeInTheDocument()
  expect(screen.getByText('<script>alert(1)</script>')).toBeInTheDocument()
  expect(container.querySelector('script')).toBeNull()
  expect(request).toHaveBeenCalledWith('/api/public/tournaments/cup', expect.objectContaining({ credentials: 'omit', cache: 'no-store' }))
})

it('retira los resultados de la vista si la publicación deja de estar disponible', async () => {
  const request = vi.fn().mockResolvedValueOnce(response(published)).mockResolvedValueOnce(response({}, 404))
  vi.stubGlobal('fetch', request)
  render(<PublicTournament id="cup" />)
  await screen.findByRole('heading', { name: 'Copa pública' })
  fireEvent.click(screen.getByRole('button', { name: 'Actualizar resultados publicados' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('no está publicado')
  expect(screen.queryByRole('heading', { name: 'Copa pública' })).not.toBeInTheDocument()
})

it('exige confirmación y versiones antes de publicar con CSRF', async () => {
  const request = vi.fn(async (url: string) => response(url === '/api/auth/csrf' ? { token: 'csrf' } : {}))
  vi.stubGlobal('fetch', request)
  const saved = vi.fn()
  render(<TournamentPublication tournament={{ id: 'cup', name: 'Copa', gameId: 'game', status: 'Finished', rulesVersion: 1, rules: null, publicationVersion: 0, lifecycleVersion: 1 }} onSaved={saved} onAccessLost={vi.fn()} />)
  expect(screen.getByRole('button', { name: 'Publicar torneo' })).toBeDisabled()
  fireEvent.click(screen.getByRole('checkbox', { name: 'Confirmo publicar los resultados del torneo' }))
  fireEvent.click(screen.getByRole('button', { name: 'Publicar torneo' }))
  await waitFor(() => expect(saved).toHaveBeenCalledOnce())
  expect(request).toHaveBeenCalledWith('/api/tournaments/cup/publication', expect.objectContaining({ body: JSON.stringify({ published: true, expectedPublicationVersion: 0, expectedLifecycleVersion: 1 }), headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }) }))
})
