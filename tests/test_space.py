import os, time
from fastcomp import space


def test_only_old_files_counted_and_cleaned(tmp_path):
    old, new = tmp_path / "old.tmp", tmp_path / "new.tmp"
    old.write_bytes(b"x" * 1000); new.write_bytes(b"y" * 1000)
    t = time.time() - 30 * 86400
    os.utime(old, (t, t))
    found = space.scan({"t": tmp_path})
    assert [p for p, _ in found["t"]] == [old]
    assert space.clean(found) == 1000
    assert not old.exists() and new.exists()
