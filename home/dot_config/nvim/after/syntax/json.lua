-- Only reached where json has no treesitter parser (Windows). The stock error
-- patterns span lines (a trailing comma is only an error if the next line is
-- `}`), but Vim redraws from the edited line down, so the line above keeps a
-- stale highlight. Start redraws a few lines earlier. jsonc pulls this in too.
vim.cmd("syntax sync linebreaks=3")
