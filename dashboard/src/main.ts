import * as Sentry from "@sentry/browser";
import { Integrations } from "@sentry/tracing";

// polyfill for replaceAll
import replaceAll from 'core-js-pure/es/string/virtual/replace-all.js';
if (!String.prototype.replaceAll)
  String.prototype.replaceAll = replaceAll;

Sentry.init({
  dsn: "https://79ba4b55fdce475ebc5d37df8b75d72a@gt.pluralkit.me/5",
  integrations: [new Integrations.BrowserTracing()],

  enabled: !window.location.origin.includes("localhost"),
  debug: false,
  // @ts-expect-error
  release: window.pluralkitVersion,
  // Set tracesSampleRate to 1.0 to capture 100%
  // of transactions for performance monitoring.
  // We recommend adjusting this value in production
  tracesSampleRate: 1.0,
});

import App from './App.svelte'

const app = new App({
  target: document.getElementById('app')
})

export default app
