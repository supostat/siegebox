local focus_count = 0

siegebox.register_app{
  id = "hello-app",
  name = "hello",
  on_launch = function(app)
    app.set_text("hello from lua!")
  end,
  on_focus = function(app)
    focus_count = focus_count + 1
    app.set_text("hello from lua! focused " .. focus_count .. " time(s)")
  end
}
