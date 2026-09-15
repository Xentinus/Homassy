import { beforeEach, describe, expect, it, vi } from 'vitest'
import { effectScope, nextTick } from 'vue'

/**
 * The lifecycle of `useSpeechRecognition` (#132).
 *
 * The drawer builds its review rows when listening ends, so what matters here is *when*
 * `isListening` falls: the engine delivers its last final result between `stop()` and its own
 * `end` event, and a session that reports itself finished before that result lands leaves the
 * drawer with an empty transcript and nothing to confirm.
 */

interface Handlers {
  onstart: (() => void) | null
  onresult: ((event: unknown) => void) | null
  onerror: ((event: { error: string }) => void) | null
  onend: (() => void) | null
}

/** A hand-driven stand-in for the browser engine — every callback fires when the test says so. */
class FakeRecognition implements Handlers {
  static instances: FakeRecognition[] = []

  lang = ''
  continuous = false
  interimResults = false
  maxAlternatives = 1
  onstart: (() => void) | null = null
  onresult: ((event: unknown) => void) | null = null
  onerror: ((event: { error: string }) => void) | null = null
  onend: (() => void) | null = null
  aborted = false
  stopped = false

  constructor() {
    FakeRecognition.instances.push(this)
  }

  start() {
    this.onstart?.()
  }

  stop() {
    this.stopped = true
  }

  abort() {
    this.aborted = true
  }

  /** Feed one result, as the engine would. */
  emit(text: string, isFinal: boolean) {
    this.onresult?.({
      resultIndex: 0,
      results: { length: 1, 0: { length: 1, isFinal, 0: { transcript: text } } }
    })
  }

  end() {
    this.onend?.()
  }
}

/** Inside an effect scope, which is what stands in for the component that would normally own it. */
const loadComposable = async () => {
  const { useSpeechRecognition } = await import('~/composables/useSpeechRecognition')
  return effectScope().run(() => useSpeechRecognition())!
}

beforeEach(() => {
  FakeRecognition.instances = []
  vi.stubGlobal('window', { SpeechRecognition: FakeRecognition })
  vi.stubGlobal('useI18n', () => ({ locale: { value: 'hu' } }))
})

describe('useSpeechRecognition', () => {
  it('stays listening until the engine ends, so the final result is not missed', async () => {
    const speech = await loadComposable()

    speech.start()
    const engine = FakeRecognition.instances[0]!
    expect(speech.isListening.value).toBe(true)

    // The reader lets go. Chrome flushes what it has *after* this returns.
    speech.stop()
    expect(engine.stopped).toBe(true)
    await nextTick()
    expect(speech.isListening.value).toBe(true)

    engine.emit('ket liter tej', true)
    engine.end()
    await nextTick()

    expect(speech.isListening.value).toBe(false)
    expect(speech.transcript.value).toBe('ket liter tej')
  })

  it('keeps the words still being revised when the engine ends without finalising them', async () => {
    const speech = await loadComposable()

    speech.start()
    const engine = FakeRecognition.instances[0]!

    engine.emit('kenyer', false)
    speech.stop()
    engine.end()
    await nextTick()

    expect(speech.transcript.value).toBe('kenyer')
    expect(speech.interim.value).toBe('')
  })

  it('throws away the utterance when the gesture is cancelled', async () => {
    const speech = await loadComposable()

    speech.start()
    const engine = FakeRecognition.instances[0]!

    engine.emit('tej', true)
    engine.emit('kenyer', false)
    speech.cancel()
    engine.end()
    await nextTick()

    expect(engine.aborted).toBe(true)
    expect(speech.isListening.value).toBe(false)
    expect(speech.transcript.value).toBe('')
    expect(speech.interim.value).toBe('')
  })

  it('starts a new session when pressed again while the previous one is winding down', async () => {
    const speech = await loadComposable()

    speech.start()
    const first = FakeRecognition.instances[0]!
    speech.stop()

    // Pressed again before the engine got around to ending the first session.
    speech.start()
    await nextTick()

    expect(FakeRecognition.instances).toHaveLength(2)
    expect(first.aborted).toBe(true)
    expect(speech.isListening.value).toBe(true)

    // The abandoned session must not speak for the new one.
    first.end()
    await nextTick()
    expect(speech.isListening.value).toBe(true)
  })
})
