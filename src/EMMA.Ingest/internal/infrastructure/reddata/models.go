package reddata

type Response struct {
	Included []IncludedItem `json:"included"`
}

type IncludedItem struct {
	Type       string     `json:"type"`
	ID         string     `json:"id"`
	Attributes Attributes `json:"attributes"`
}

type Attributes struct {
	Title  string  `json:"title"`
	Values []Value `json:"values"`
}

type Value struct {
	Value    float64 `json:"value"`
	Datetime string  `json:"datetime"`
}
