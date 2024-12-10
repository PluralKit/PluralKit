package main

import (
	"embed"
	"encoding/json"
	"errors"
	"fmt"
	"html"
	"io"
	_fs "io/fs"
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
			// only index root and status pages
			if r.URL.Path != "/" && r.URL.Path != "/status" {
				rw.Header().Set("X-Robots-Tag", "noindex")
			}

			rw.Header().Set("X-PluralKit-Version", version)
			next.ServeHTTP(rw, r)
		})
	})

	r.Get("/robots.txt", func(rw http.ResponseWriter, r *http.Request) {
		rw.Write([]byte("User-Agent: *\nAllow: /$\nAllow: /status\nDisallow: /\n"))
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
	if r.URL.Path == "/myriad.png" {
		data, err = fs.ReadFile("dist/myriad.png")
		rw.Header().Set("content-type", "image/png")
	} else if strings.HasSuffix(r.URL.Path, ".js") {
		data, err = fs.ReadFile("dist" + r.URL.Path)
		rw.Header().Set("content-type", "application/javascript")
	} else if strings.HasSuffix(r.URL.Path, ".css") {
		data, err = fs.ReadFile("dist" + r.URL.Path)
		rw.Header().Set("content-type", "text/css")
	} else if strings.HasSuffix(r.URL.Path, ".map") {
		data, err = fs.ReadFile("dist" + r.URL.Path)
	} else if strings.HasSuffix(r.URL.Path, ".ttf") {
		data, err = fs.ReadFile("dist" + r.URL.Path)
		rw.Header().Set("content-type", "application/x-font-ttf")
	} else if strings.HasSuffix(r.URL.Path, ".woff2") {
		data, err = fs.ReadFile("dist" + r.URL.Path)
		rw.Header().Set("content-type", "application/font-woff2")
	} else {
		data, err = fs.ReadFile("dist/index.html")
		rw.Header().Set("content-type", "text/html")
		data = []byte(strings.Replace(string(data), `<!-- extra data -->`, defaultEmbed+versionJS, 1))
	}

	if errors.Is(err, _fs.ErrNotExist) {
		rw.WriteHeader(http.StatusNotFound)
	} else if err != nil {
		rw.WriteHeader(http.StatusInternalServerError)
	} else {
		// cache built+hashed dashboard js/css files forever
		is_dash_static_asset := strings.HasPrefix(r.URL.Path, "/assets/") &&
			(strings.HasSuffix(r.URL.Path, ".js") || strings.HasSuffix(r.URL.Path, ".css") || strings.HasSuffix(r.URL.Path, ".map"))

		if is_dash_static_asset {
			rw.Header().Add("Cache-Control", "max-age=31536000, s-maxage=31536000, immutable")
		}

		rw.Write(data)
	}
}

// explanation for createEmbed:
// we don't care about errors, we just want to return a HTML page as soon as possible
// `panic(1)` is caught by upstream, which then returns the raw HTML page

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
		panic(1)
	}

	res, err := http.Get(baseURL + path)
	if err != nil {
		panic(1)
	}
	if res.StatusCode != 200 {
		panic(1)
	}

	var data entity
	body, _ := io.ReadAll(res.Body)
	err = json.Unmarshal(body, &data)
	if err != nil {
		panic(1)
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
		panic(1)
	}
	html = []byte(strings.Replace(string(html), `<!-- extra data -->`, text+versionJS, 1))

	rw.Header().Add("content-type", "text/html")
	rw.Write(html)
}
