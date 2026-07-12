siegebox.register_command("hello", function(ctx)
  local target = ctx.args[1] or "world"
  ctx.write("hello, " .. target .. "!\n")
  return 0
end)
