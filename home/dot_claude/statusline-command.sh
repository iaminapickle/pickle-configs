#!/bin/sh
jq -r '
  .model.display_name // "?" as $model
  | ((.context_window.total_input_tokens // 0) + (.context_window.total_output_tokens // 0)) as $tokens
  | (.context_window.used_percentage // 0) as $pct
  | (.context_window.current_usage.cache_read_input_tokens // 0) as $cache_hit
  | (.cost.total_cost_usd // 0) as $cost
  | "\($model) | Tokens: \($tokens / 1000 | floor)k (\($pct)%) | LastCacheHit: \($cache_hit / 1000 | floor)k | Cost: $\(($cost * 100 | round) / 100)"
'
