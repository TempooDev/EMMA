package reddata

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"time"
)

const BaseURL = "https://apidatos.ree.es/es/datos"

type Client struct {
	httpClient *http.Client
}

func NewClient() *Client {
	return &Client{
		httpClient: &http.Client{Timeout: 10 * time.Second},
	}
}

// FetchData makes a request to the REE API
// category: e.g. "mercados"
// widget: e.g. "precios-mercados-tiempo-real"
func (c *Client) FetchData(category, widget string, start, end time.Time, timeTrunc string) (*Response, error) {
	// Construct URL
	// Pattern: [category]/[indicator]?start_date=[ISO8601]&end_date=[ISO8601]&time_trunc=[hour/day/month]
	path := fmt.Sprintf("%s/%s/%s", BaseURL, category, widget)
	
	u, err := url.Parse(path)
	if err != nil {
		return nil, fmt.Errorf("invalid url: %w", err)
	}

	params := url.Values{}
	// ISO8601 format example: 2006-01-02T15:04
	params.Add("start_date", start.Format("2006-01-02T15:04")) 
	params.Add("end_date", end.Format("2006-01-02T15:04"))
	if timeTrunc != "" {
		params.Add("time_trunc", timeTrunc)
	}

	u.RawQuery = params.Encode()
	
	fmt.Printf("Requesting: %s\n", u.String())

	req, err := http.NewRequest("GET", u.String(), nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}

	req.Header.Set("Accept", "application/json")
	req.Header.Set("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36")


	resp, err := c.httpClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("request failed: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		// efficient enough for error case
		buf := new(strings.Builder)
		_, _ = io.Copy(buf, resp.Body)
		return nil, fmt.Errorf("API status: %d, Content-Type: %s, body: %s", resp.StatusCode, resp.Header.Get("Content-Type"), buf.String())
	}

	var result Response
	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		return nil, fmt.Errorf("failed to decode response: %w", err)
	}

	return &result, nil
}
