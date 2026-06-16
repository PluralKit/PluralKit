use super::*;

pub fn cmds() -> impl IntoIterator<Item = Command> {
    [
        command!("thunder" => "fun_thunder").help("Vanquishes your opponent with a lightning bolt"),
        command!("meow" => "fun_meow").help("mrrp :3"),
        command!("mn" => "fun_pokemon").help("Gotta catch 'em all!"),
        command!("fire" => "fun_fire").help("Engulfs your opponent in a pillar of fire"),
        command!("freeze" => "fun_freeze").help("Freezes your opponent solid"),
        command!("starstorm" => "fun_starstorm")
            .help("Summons a storm of meteors to strike your opponent"),
        command!("flash" => "fun_flash").help("Explodes your opponent with a ball of green light"),
        command!("rool" => "fun_rool").help("\"What the fuck is a Pokémon?\""),
        command!("sus" => "amogus").help("ඞ"),
        command!("error" => "fun_error").help("Shows a fake error message"),
    ]
}
