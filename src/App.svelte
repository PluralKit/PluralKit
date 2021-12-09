<script lang="ts">
  import { onMount } from "svelte";
  import { Router, Link, Route } from "svelte-navigator";
  import { Form, Input } from 'sveltestrap';
  import Toggle from 'svelte-toggle';
  import Navigation from "./lib/Navigation.svelte";

  let light = "https://cdn.jsdelivr.net/npm/bootstrap@5.1.0/dist/css/bootstrap.min.css"
  let dark = "https://cdn.jsdelivr.net/npm/bootstrap-dark-5@1.1.3/dist/css/bootstrap-night.min.css"

  let styleSrc = light;
  let style = localStorage.getItem("pk-style"); 
  
  onMount(() => {
    setStyle(style);
  })

  function setStyle(style) {
    switch (style) {
      case "light": styleSrc = light;
      localStorage.setItem("pk-style", "light");
      break;
      case "dark": styleSrc = dark;
      localStorage.setItem("pk-style", "dark");
      break;
      default: styleSrc = light;
      localStorage.setItem("pk-style", "light");
    };
  };

  function styleSelect(e) {
    style = e.target.value.toLowerCase();
    setStyle(style);
  };
</script>

<svelte:head>
  <link rel="stylesheet" href={styleSrc}>
</svelte:head>

<Router>
  <Navigation/>
  <div>
    <Route path="/">
        <h2>Ooga booga</h2>
        <Form>
          <Input type="select" name="themes" id="themes" on:change={(e) => styleSelect(e)}>
            <option>Light</option>
            <option>Dark</option>
          </Input>
        </Form>
    </Route>
  </div>
</Router>