"""Dated-artifact convention for tools/outputs products (2026-09-20, user ruling).

Every recurring tools product is written twice:
  1. the fixed-path alias consumers read (e.g. unity_cards_current.json);
  2. a dated twin <stem>_<YYYYMMDD_HHMMSS><ext> — the uploadable, browsable artifact.

Generators keep writing their usual path, then call alias_copy() once.
Run from the repo root (all tools do).
"""
import datetime
import os
import shutil


def stamp():
	return datetime.datetime.now().strftime("%Y%m%d_%H%M%S")


def dated_path(current_path, ts=None):
	# unity_cards_current.json -> unity_cards_20260920_203813.json;
	# files without the _current suffix keep their full stem (catalog_versions.tsv).
	ts = ts or stamp()
	d, f = os.path.split(current_path)
	stem, ext = os.path.splitext(f)
	if stem.endswith("_current"):
		stem = stem[:-len("_current")]
	return os.path.join(d, "%s_%s%s" % (stem, ts, ext))


def alias_copy(current_path, ts=None):
	"""Copy the just-written alias to its dated twin; returns the twin path."""
	dated = dated_path(current_path, ts)
	shutil.copyfile(current_path, dated)
	return dated
