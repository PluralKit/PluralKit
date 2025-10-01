use super::*;

pub fn cmds() -> impl Iterator<Item = Command> {
    [
        command!("thunder" => "fun_thunder"),
        command!("meow" => "fun_meow"),
        command!("mn" => "fun_pokemon"),
        command!("fire" => "fun_fire"),
        command!("freeze" => "fun_freeze"),
        command!("starstorm" => "fun_starstorm"),
        command!("flash" => "fun_flash"),
        command!("rool" => "fun_rool"),
        command!("sus" => "amogus"),
        command!("error" => "fun_error"),
    ]
    .into_iter()
}
