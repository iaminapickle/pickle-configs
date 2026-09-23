local languages = {
  "lua", "vim", "vimdoc", "bash", "python", "c", "cpp",
  "json", "yaml", "markdown", "markdown_inline", "gitcommit", "diff",
}

-- Parsers Neovim ships prebuilt in $VIMRUNTIME. Everything else is compiled on
-- demand by the tree-sitter CLI, which needs a C toolchain.
local bundled = {
  c = true, lua = true, markdown = true, markdown_inline = true,
  query = true, vim = true, vimdoc = true,
}

-- The tree-sitter CLI is an MSVC-target build, so on Windows it shells out to
-- cl.exe with MSVC flags -- zig/gcc can't stand in. Rather than drag in Visual
-- Studio Build Tools, fall back to the bundled parsers there.
local buildable = vim.fn.has("win32") == 0

local enabled = buildable and languages
  or vim.tbl_filter(function(lang)
    return bundled[lang]
  end, languages)

return {
  "nvim-treesitter/nvim-treesitter",
  branch = "main",
  lazy = false,
  build = buildable and ":TSUpdate" or nil,
  config = function()
    if buildable then
      require("nvim-treesitter").install(languages)
    end

    vim.api.nvim_create_autocmd("FileType", {
      pattern = enabled,
      callback = function()
        vim.treesitter.start()
        vim.wo[0][0].foldexpr = "v:lua.vim.treesitter.foldexpr()"
        vim.bo.indentexpr = "v:lua.require'nvim-treesitter'.indentexpr()"
      end,
    })
  end,
}
