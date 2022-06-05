import * as Sentry from "@sentry/browser";
import { Integrations } from "@sentry/tracing";

Sentry.init({
  dsn: "https://58109fec589f4c2bbfa190329acf679a@sentry.pluralkit.me/4",
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
