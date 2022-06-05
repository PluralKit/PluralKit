package main

import (
	"embed"
	"encoding/json"
	"fmt"
	"html"
	"io"
	"net/http"
	"strings"

	"github.com/go-chi/chi"
)

//go:embed dist/*
var fs embed.FS

type entity struct {
	AvatarURL   *string `json:"avatar_url"`
	IconURL     *string `json:"icon_url"`
	Description *string `json:"description"`
	Color       *string `json:"color"`
}

var baseURL = "https://api.pluralkit.me/v2"

var version = "dev"
var versionJS string

const defaultEmbed = `<meta property="og:title" content="PluralKit | web dashboard" /> <meta name="theme-color" content="#da9317">`

func main() {
	versionJS = "<script>window.pluralkitVersion = '" + version + "'</script>"

	r := chi.NewRouter()

	r.Use(func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(rw http.ResponseWriter, r *http.Request) {
			rw.Header().Set("X-PluralKit-Version", version)
			next.ServeHTTP(rw, r)
		})
	})

	r.NotFound(notFoundHandler)

	r.Get("/profile/{type}/{id}", func(rw http.ResponseWriter, r *http.Request) {
		defer func() {
			if a := recover(); a != nil {
				notFoundHandler(rw, r)
				return
			}
		}()
		createEmbed(rw, r)
	})

	http.ListenAndServe(":8080", r)
}

func notFoundHandler(rw http.ResponseWriter, r *http.Request) {
	var data []byte
	var err error

	// lol
	if strings.HasSuffix(r.URL.Path, ".js") {
		data, err = fs.ReadFile("dist" + r.URL.Path)
		rw.Header().Add("content-type", "application/javascript")
	} else if strings.HasSuffix(r.URL.Path, ".css") {
		data, err = fs.ReadFile("dist" + r.URL.Path)
		rw.Header().Add("content-type", "text/css")
	} else if strings.HasSuffix(r.URL.Path, ".map") {
		data, err = fs.ReadFile("dist" + r.URL.Path)
	} else {
		data, err = fs.ReadFile("dist/index.html")
		rw.Header().Add("content-type", "text/html")
		data = []byte(strings.Replace(string(data), `<!-- extra data -->`, defaultEmbed+versionJS, 1))
	}

	if err != nil {
		panic(err)
	}

	rw.Write(data)
}

// explanation for createEmbed:
// we don't care about errors, we just want to return a HTML page as soon as possible
// `panic(nil)` is caught by upstream, which then returns the raw HTML page

func createEmbed(rw http.ResponseWriter, r *http.Request) {
	entityType := chi.URLParam(r, "type")
	id := chi.URLParam(r, "id")

	var path string

	switch entityType {
	case "s":
		path = "/systems/" + id
	case "m":
		path = "/members/" + id
	case "g":
		path = "/groups/" + id
	default:
		panic(nil)
	}

	res, err := http.Get(baseURL + path)
	if err != nil {
		panic(nil)
	}
	if res.StatusCode != 200 {
		panic(nil)
	}

	var data entity
	body, _ := io.ReadAll(res.Body)
	err = json.Unmarshal(body, &data)
	if err != nil {
		panic(nil)
	}

	text := fmt.Sprintf(`<link type="application/json+oembed" href="%s/%s/oembed.json" />%s`, baseURL, path, "\n")

	if data.AvatarURL != nil {
		text += fmt.Sprintf(`<meta content='%s' property='og:image'>%s`, html.EscapeString(*data.AvatarURL), "\n")
	} else if data.IconURL != nil {
		text += fmt.Sprintf(`<meta content='%s' property='og:image'>%s`, html.EscapeString(*data.IconURL), "\n")
	}

	if data.Description != nil {
		text += fmt.Sprintf(`<meta content="%s" property="og:description">%s`, html.EscapeString(*data.Description), "\n")
	}

	if data.Color != nil {
		text += fmt.Sprintf(`<meta name="theme-color" content="#%s">%s`, html.EscapeString(*data.Color), "\n")
	}

	html, err := fs.ReadFile("dist/index.html")
	if err != nil {
		panic(nil)
	}
	html = []byte(strings.Replace(string(html), `<!-- extra data -->`, text+versionJS, 1))

	rw.Header().Add("content-type", "text/html")
	rw.Write(html)
}
