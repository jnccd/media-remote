# NixOS module for the MediaControl desktop wrapper.
#
# The MediaControl server injects keyboard/mouse input globally. On Linux/Wayland that is
# done through uinput + ydotool (NOT X11/XTest, which is what SharpHook uses and which only
# reaches XWayland apps). Because global input injection needs kernel module + user group
# privileges, this *cannot* live in a package — it has to be wired into the system config.
#
# Enable it in your configuration.nix / flake module list:
#
#   services.mediaControl = {
#     enable = true;
#     users  = [ "yourUsername" ];   # accounts allowed to inject input
#   };
#
# What it does:
#   - loads the `uinput` kernel module (ydotoold talks to /dev/uinput)
#   - udev rule so the `input` group can open /dev/uinput
#   - ensures the `input` group exists and adds the listed users to it
#   - installs `ydotool`
#   - runs `ydotoold` (the ydotool daemon) as a user service on login

{ config, lib, pkgs, ... }:

let
  cfg = config.services.mediaControl;
  uinputRule = ''
    SUBSYSTEM=="misc", KERNEL=="uinput", GROUP="input", MODE="0660"
  '';
in
{
  options.services.mediaControl = {
    enable = lib.mkEnableOption "MediaControl desktop wrapper (server, tray, global input injection)";

    users = lib.mkOption {
      type = lib.types.listOf lib.types.str;
      default = [ ];
      description = ''
        User accounts to add to the `input` group so the MediaControl server can inject
        keyboard/mouse input through uinput/ydotool.
      '';
    };
  };

  config = lib.mkIf cfg.enable {
    boot.kernelModules = [ "uinput" ];

    # The `input` group needs access to /dev/uinput for ydotoold to write to it.
    services.udev.extraRules = uinputRule;

    users.groups.input = { };

    users.users = lib.mkMerge (map (user: {
      ${user} = { extraGroups = [ "input" ]; };
    }) cfg.users);

    environment.systemPackages = [ pkgs.ydotool ];

    # Run the ydotool daemon as the logged-in user so both the daemon (ydotoold) and the
    # server's client (ydotool) share the same user/socket and the same `input` group.
    systemd.user.services.ydotoold = {
      description = "ydotool daemon (MediaControl input injection)";
      wantedBy = [ "default.target" ];
      serviceConfig = {
        Type = "simple";
        ExecStart = "${pkgs.ydotool}/bin/ydotoold";
        Restart = "on-failure";
        RestartSec = "2";
      };
    };
  };
}
