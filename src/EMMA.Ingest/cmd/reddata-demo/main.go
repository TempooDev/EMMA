package main

import (
	"fmt"
	"log"
	"time"

	"emma/ingestor/internal/infrastructure/reddata"
)

func main() {
	client := reddata.NewClient()
	// Use 2024 dates to ensure data availability (avoid future year issues)
	start := time.Date(2024, 1, 1, 0, 0, 0, 0, time.UTC)
	end := time.Date(2024, 1, 2, 0, 0, 0, 0, time.UTC)


	fmt.Println("=== REDData API Integration Demo ===")

	// 1. PVPC
	fetchAndPrint(client, "mercados", "precios-mercados-tiempo-real", start, end, "hour")
	
	// 2. Generation Mix
	fetchAndPrint(client, "generacion", "estructura-generacion", start, end, "hour")

	// 3. Demand
	fetchAndPrint(client, "demanda", "evolucion", start, end, "hour")

	// 4. CO2 Emissions
	fetchAndPrint(client, "generacion", "no-renovables-detalle-emisiones", start, end, "hour")
}

func fetchAndPrint(client *reddata.Client, category, widget string, start, end time.Time, timeTrunc string) {
	fmt.Printf("\n--- Fetching %s/%s ---\n", category, widget)
	resp, err := client.FetchData(category, widget, start, end, timeTrunc)
	if err != nil {
		log.Printf("Error: %v\n", err)
		return
	}

	for _, included := range resp.Included {
		fmt.Printf("Indicator: %s\n", included.Attributes.Title)
		for _, v := range included.Attributes.Values {
			// Parsing logic
			t, err := time.Parse("2006-01-02T15:04:05.000-07:00", v.Datetime)
			if err != nil {
				t, err = time.Parse("2006-01-02T15:04:05", v.Datetime)
				if err != nil {
					fmt.Printf("  Error parsing date %s\n", v.Datetime)
					continue
				}
			}
			fmt.Printf("  %s | %.3f\n", t.Format("2006-01-02 15:04"), v.Value)
		}
	}
}
