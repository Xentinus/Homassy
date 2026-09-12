import { computed, onBeforeUnmount, ref, shallowRef } from 'vue'

/**
 * A thin wrapper over the Web Speech API's recognition half (#132).
 *
 * Two things about it shape this file. It is **not** in every browser — Firefox has no
 * implementation at all — so support has to be detected and the affordance hidden rather than
 * offered and then failed. And on some platforms (Chrome on the desktop, Safari) the audio is
 * sent to the vendor's servers to be transcribed, which is why the UI says so and why the
 * feature stays entirely optional.
 *
 * The vendor-prefixed name is still the only one Safari answers to, so both are looked up.
 */

/** What went wrong, reduced to the cases the UI actually says something different about. */
export type SpeechRecognitionFailure = 'denied' | 'no-speech' | 'network' | 'unknown'

// The Web Speech API is not in TypeScript's DOM library. These are the parts this file touches,
// declared locally rather than pulled in as a dependency for one feature.
interface SpeechRecognitionAlternativeLike { transcript: string }
interface SpeechRecognitionResultLike {
  readonly length: number
  isFinal: boolean
  [index: number]: SpeechRecognitionAlternativeLike
}
interface SpeechRecognitionEventLike {
  resultIndex: number
  results: { readonly length: number, [index: number]: SpeechRecognitionResultLike }
}
interface SpeechRecognitionErrorEventLike { error: string }
interface SpeechRecognitionLike {
  lang: string
  continuous: boolean
  interimResults: boolean
  maxAlternatives: number
  start: () => void
  stop: () => void
  abort: () => void
  onresult: ((event: SpeechRecognitionEventLike) => void) | null
  onerror: ((event: SpeechRecognitionErrorEventLike) => void) | null
  onend: (() => void) | null
  onstart: (() => void) | null
}
type SpeechRecognitionConstructor = new () => SpeechRecognitionLike

const getConstructor = (): SpeechRecognitionConstructor | null => {
  if (!import.meta.client) return null
  const scope = window as unknown as {
    SpeechRecognition?: SpeechRecognitionConstructor
    webkitSpeechRecognition?: SpeechRecognitionConstructor
  }
  return scope.SpeechRecognition ?? scope.webkitSpeechRecognition ?? null
}

/** The app's locale codes as BCP 47 tags, which is what `SpeechRecognition.lang` wants. */
const SPEECH_LANGUAGES: Record<string, string> = {
  en: 'en-GB',
  hu: 'hu-HU',
  de: 'de-DE'
}

export const useSpeechRecognition = () => {
  const { locale } = useI18n()

  /** False during SSR and in every browser without the API — gate the affordance on it. */
  const isSupported = computed(() => getConstructor() !== null)

  const isListening = ref(false)
  /** Everything heard so far this session, final results only. */
  const transcript = ref('')
  /** The words currently being revised — shown live, never kept. */
  const interim = ref('')
  const failure = ref<SpeechRecognitionFailure | null>(null)

  const recognition = shallowRef<SpeechRecognitionLike | null>(null)
  // A stop we asked for, so `onend` does not read as the engine giving up.
  let stoppedByUs = false

  const mapError = (code: string): SpeechRecognitionFailure => {
    if (code === 'not-allowed' || code === 'service-not-allowed') return 'denied'
    if (code === 'no-speech') return 'no-speech'
    if (code === 'network') return 'network'
    return 'unknown'
  }

  const teardown = () => {
    const instance = recognition.value
    if (!instance) return

    instance.onresult = null
    instance.onerror = null
    instance.onend = null
    instance.onstart = null
    recognition.value = null
  }

  /**
   * Start listening. Resets the transcript — one press is one utterance.
   *
   * The browser asks for the microphone the first time this runs and remembers the answer, so
   * there is nothing to request up front: a permission prompt with no context is worse than one
   * that appears the moment the reader presses the button.
   */
  const start = () => {
    const Recognition = getConstructor()
    if (!Recognition || isListening.value) return

    transcript.value = ''
    interim.value = ''
    failure.value = null
    stoppedByUs = false

    const instance = new Recognition()
    instance.lang = SPEECH_LANGUAGES[locale.value] ?? SPEECH_LANGUAGES.en!
    // Continuous, because a shopping list is read out as one long breath with pauses in it, and
    // a single-shot recognizer stops at the first one.
    instance.continuous = true
    instance.interimResults = true
    instance.maxAlternatives = 1

    instance.onstart = () => {
      isListening.value = true
    }

    instance.onresult = (event) => {
      let pending = ''

      for (let index = event.resultIndex; index < event.results.length; index++) {
        const result = event.results[index]
        if (!result) continue
        const text = result[0]?.transcript ?? ''
        if (result.isFinal) transcript.value = `${transcript.value} ${text}`.trim()
        else pending += text
      }

      interim.value = pending
    }

    instance.onerror = (event) => {
      // "no-speech" while the engine is still running is not a failure worth showing — it fires
      // during any pause long enough for the engine to notice. Only report it if nothing at all
      // was heard by the time listening ends.
      const mapped = mapError(event.error)
      if (mapped === 'no-speech' && transcript.value) return
      failure.value = mapped
    }

    instance.onend = () => {
      isListening.value = false
      interim.value = ''
      teardown()

      // Some engines end by themselves after a long pause. Nothing to report if the reader let
      // go; if they did not, and nothing was heard, say so.
      if (!stoppedByUs && !transcript.value && !failure.value) failure.value = 'no-speech'
    }

    recognition.value = instance

    try {
      instance.start()
    } catch {
      // Starting an instance that is already running throws. Nothing has been lost — the
      // existing session is still listening.
      teardown()
    }
  }

  /** Stop listening and keep what was heard. */
  const stop = () => {
    stoppedByUs = true
    isListening.value = false
    recognition.value?.stop()
  }

  /** Stop listening and throw away what was heard — the release-outside gesture. */
  const cancel = () => {
    stoppedByUs = true
    isListening.value = false
    transcript.value = ''
    interim.value = ''
    recognition.value?.abort()
  }

  const reset = () => {
    transcript.value = ''
    interim.value = ''
    failure.value = null
  }

  onBeforeUnmount(() => {
    stoppedByUs = true
    recognition.value?.abort()
    teardown()
  })

  return {
    isSupported,
    isListening,
    transcript,
    interim,
    failure,
    start,
    stop,
    cancel,
    reset
  }
}
