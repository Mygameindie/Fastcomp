import json
from pathlib import Path

from fastcomp import core, linux


def make_sys(root: Path):
    pol = root / "sys/devices/system/cpu/cpufreq/policy0"
    pol.mkdir(parents=True)
    (pol / "energy_performance_preference").write_text("balance_performance\n")
    return pol


def test_apply_and_restore_roundtrip(tmp_path, monkeypatch):
    monkeypatch.setenv("FASTCOMP_STATE", str(tmp_path / "state.json"))
    pol = make_sys(tmp_path)
    s = linux.settings("battery", tmp_path)

    plan = core.apply(s, dry_run=True)
    assert plan[0].status == "would-change"
    assert (pol / "energy_performance_preference").read_text().strip() == "balance_performance"
    assert not (tmp_path / "state.json").exists()

    assert core.apply(s, dry_run=False)[0].status == "changed"
    assert (pol / "energy_performance_preference").read_text() == "balance_power"
    assert core.apply(s, dry_run=False)[0].status == "ok"   # idempotent

    # switching profile must not overwrite the saved original
    core.apply(linux.settings("performance", tmp_path), dry_run=False)
    assert json.loads((tmp_path / "state.json").read_text())["originals"][
        "sys/devices/system/cpu/cpufreq/policy0/energy_performance_preference"] == "balance_performance"

    assert core.restore(s)[0].status == "restored"
    assert (pol / "energy_performance_preference").read_text() == "balance_performance"
    assert core.load_state() == {}


def test_failure_is_reported_not_raised(tmp_path, monkeypatch):
    monkeypatch.setenv("FASTCOMP_STATE", str(tmp_path / "state.json"))
    def boom(v): raise PermissionError("denied")
    s = core.Setting("x", "x", lambda: "a", boom, "b", admin=True)
    r = core.apply([s], dry_run=False)[0]
    assert r.status == "failed" and "root" in r.detail
    assert core.load_state() == {}
