#!/usr/bin/env python3
"""Verify release structure. Compilation and gameplay testing are separate checks."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
metadata = ET.parse(root / "About/About.xml").getroot()
assert metadata.tag == "ModMetaData"
assert metadata.findtext("packageId") == "pengin0503.colonytaskauditor"
assert metadata.findtext("supportedVersions/li") == "1.7"

proj = ET.parse(root / "Source/ColonyTaskAuditor.csproj").getroot()
assert proj.findtext(".//TargetFramework") == "net10.0"
assert any(p.attrib.get("Include") == "Krafs.Rimworld.Ref" for p in proj.findall(".//PackageReference"))
source = "\n".join(p.read_text(encoding="utf-8") for p in (root / "Source").glob("*.cs"))
assert source.count("{") == source.count("}"), "Brace count mismatch (not a compiler check)"
assert "ModSettings" in source and "MapComponent" in source
used = set(re.findall(r'"(CTA_[A-Za-z0-9]+)"[.]Translate[(]', source))
used |= {"CTA_"+k for k in ("Unknown","Confirmed","Likely","Uncertain","Paused","Waiting","Forbidden","NoWorker","BillRestriction","Working","Unreachable")}
used |= {"CTA_Filter"+k for k in ("all","designation","bill","grow","unfinished","gather")}
used |= {"CTA_MaterialMissing","CTA_MaterialDelivery","CTA_Reserved"}
used |= {"CTA_Skill","CTA_BillMaterials"}
languages = []
for language in ("English", "Japanese"):
    data = ET.parse(root / "Languages" / language / "Keyed/Strings.xml").getroot()
    assert data.tag == "LanguageData"
    keys = [n.tag for n in data]
    assert len(keys) == len(set(keys)), "Duplicate translation keys"
    assert used <= set(keys), f"Missing {language}: {used-set(keys)}"
    assert all(n.text and n.text.strip() for n in data), "Empty translation"
    languages.append(set(keys))
assert languages[0] == languages[1], "Translation keys differ"
print(f"OK: ColonyTaskAuditor metadata and bilingual keys ({len(used)} used)")

