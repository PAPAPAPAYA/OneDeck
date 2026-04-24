import os, re, glob
from datetime import datetime

ROOT = r'd:\Unity Projects\OneDeck'
CARDS = os.path.join(ROOT, 'Assets', 'Prefabs', 'Cards', '3.0 no cost (current)')
EVENTS = os.path.join(ROOT, 'Assets', 'SORefs', 'GameEvents')
DOCS = os.path.join(ROOT, 'docs')

SE_NAMES = ['None','Infected','Mana','HeartChanged','Power','Rest','Revive','Counter']
TAG_NAMES = ['None','Linger','ManaX','DeathRattle']
TARGET_TYPE_NAMES = ['Me','Them','Random']

def hex_list(s):
    if not s: return []
    s=s.strip().replace(' ','').replace('\n','')
    if len(s)%8!=0:
        try: return [int(s)]
        except: return []
    r=[]
    for i in range(0,len(s),8):
        r.append(int.from_bytes(bytes.fromhex(s[i:i+8]),'little',signed=True))
    return r

def fget(txt,fn):
    m=re.search(r'^  '+re.escape(fn)+r':\s*(.*?)$',txt,re.M)
    if not m:
        return ''
    val=m.group(1).strip()
    start=m.end()
    lines=txt[start:].split('\n')
    for line in lines:
        if line.startswith('    '):
            val=val+' '+line.strip()
        elif line.strip()=='' or line.startswith('  ') and not line.startswith('    ') and line.strip():
            if line.strip()=='':
                continue
            else:
                break
        else:
            break
    return val

def fgetall(txt):
    return {m.group(1):m.group(2).strip() for m in re.finditer(r'^  (\w+):[ \t]*(.*?)$',txt,re.M)}

def calls(txt,prop):
    r=[]
    # Match the event property and capture everything until the next top-level property (2-space key) or end of string
    m=re.search(r'  '+re.escape(prop)+r':\n    m_PersistentCalls:\n      m_Calls:(.*?)(?=\n  \w+|\Z)',txt,re.DOTALL)
    if not m: return r
    section=m.group(1)
    # If empty array, return empty
    if section.strip().startswith('[]') or not section.strip():
        return r
    call_blocks=re.findall(r'      - m_Target:.*?m_CallState: \d',section,re.DOTALL)
    for c in call_blocks:
        tt_match=re.search(r'm_TargetAssemblyTypeName:\s*(.+)',c)
        mn=re.search(r'm_MethodName:\s*(.+?)(?:\n|$)',c)
        ia=re.search(r'm_IntArgument:\s*(\d+)',c)
        sa=re.search(r'm_StringArgument: (.*)',c)
        if tt_match:
            t=tt_match.group(1).strip()
            remaining=c[tt_match.end():]
            nlm=re.match(r'\n\s+(\S.*)',remaining)
            if nlm and t.endswith(','):
                t=t+' '+nlm.group(1).strip()
        else:
            t=''
        t=t.replace('\n',' ').replace('  ',' ').strip()
        t=re.sub(r',\s*Assembly-CSharp$','',t)
        r.append({'target_type':t,'method':(mn.group(1).strip() if mn else ''),'int_arg':int(ia.group(1)) if ia else 0,'string_arg':(sa.group(1).strip() if sa else '')})
    return r

def parse(path):
    with open(path,'r',encoding='utf-8') as f: content=f.read()
    blocks=re.split(r'--- !u!(\d+) &([\d -]+)\n',content)
    objs={}; gos={}; txs={}; mbs=[]
    for i in range(1,len(blocks),3):
        if i+2>=len(blocks): break
        ot=blocks[i]; fid=blocks[i+1].strip(); bt=blocks[i+2]
        cn=bt.strip().split('\n')[0].rstrip(':')
        obj={'type':ot,'fileID':fid,'className':cn,'text':bt}
        objs[fid]=obj
        if cn=='GameObject':
            obj['name']=fget(bt,'m_Name')
            obj['components']=re.findall(r'component:\s*\{fileID:\s*([\d -]+)\}',bt)
            gos[fid]=obj
        elif cn=='Transform':
            obj['gameObject']=fget(bt,'m_GameObject')
            obj['father']=fget(bt,'m_Father')
            obj['children']=re.findall(r'- \{fileID:\s*([\d -]+)\}',bt)
            txs[fid]=obj
        elif cn=='MonoBehaviour':
            obj['gameObject']=fget(bt,'m_GameObject')
            obj['fields']=fgetall(bt)
            mbs.append(obj)
    rid=None
    for t in txs.values():
        if t['father']=='{fileID: 0}':
            rid=t['gameObject']; break
    if not rid: return None
    ridc=rid.replace('{fileID: ','').replace('}','')
    rgo=gos.get(ridc)
    if not rgo: return None
    cs=None
    cs_mb=None
    for mb in mbs:
        if mb['gameObject']==rid and 'cardTypeID' in mb['fields']:
            cs=mb['fields']; cs_mb=mb; break
    if not cs: return None
    # Re-extract multi-line fields
    if cs_mb:
        cs['displayName']=fget(cs_mb['text'],'displayName')
        cs['cardDesc']=fget(cs_mb['text'],'cardDesc')
    sl=hex_list(cs.get('myStatusEffects',''))
    tl=hex_list(cs.get('myTags',''))
    lmap={}
    for mb in mbs:
        if mb['gameObject']==rid and 'event' in mb['fields']:
            g=re.search(r'guid:\s*([a-f0-9]+)',mb['fields'].get('event',''))
            if g:
                cbs=re.findall(r'      - m_Target:.*?m_CallState: \d',mb['text'],re.DOTALL)
                for c in cbs:
                    tm=re.search(r'm_Target:\s*\{fileID:\s*([\d -]+)\}',c)
                    if tm and 'CostNEffectContainer' in c:
                        lmap[tm.group(1)]=g.group(1)
    rt=None
    for t in txs.values():
        if t['gameObject']==rid: rt=t; break
    cons=[]; effs=[]
    if rt:
        for ctid in rt.get('children',[]):
            ct=txs.get(ctid)
            if not ct: continue
            cgo=ct['gameObject'].replace('{fileID: ','').replace('}','')
            cgo_obj=gos.get(cgo)
            if not cgo_obj: continue
            cname=cgo_obj['name']
            for mb in mbs:
                if mb['gameObject']==ct['gameObject']:
                    mf=mb['fields']
                    if 'checkCostEvent' in mf:
                        cons.append({'name':cname,'trigger_guid':lmap.get(mb['fileID']),'check_cost':calls(mb['text'],'checkCostEvent'),'pre_effect':calls(mb['text'],'preEffectEvent'),'effect':calls(mb['text'],'effectEvent')})
                    elif 'baseDmg' in mf:
                        bdl=mf.get('baseDmg','')
                        bd='2' if 'fileID:' in bdl and bdl!='{fileID: 0}' else '0'
                        effs.append({'type':'HPAlter','name':cname,'baseDmg':bd,'extraDmg':mf.get('extraDmg','0'),'isStatusEffectDamage':mf.get('isStatusEffectDamage','0'),'statusEffectToCheck':mf.get('statusEffectToCheck','0')})
                    elif 'tagToCheck' in mf and 'targetFriendly' in mf:
                        effs.append({'type':'Stage','name':cname,'tagToCheck':mf.get('tagToCheck','0'),'targetFriendly':mf.get('targetFriendly','0'),'statusEffectToCheck':mf.get('statusEffectToCheck','0')})
                    elif 'tagToCheck' in mf and 'cardTypeID' not in mf:
                        cid=mf.get('m_EditorClassIdentifier','')
                        if 'BuryEffect' in cid: effs.append({'type':'Bury','name':cname,'tagToCheck':mf.get('tagToCheck','0')})
                        elif 'ExileEffect' in cid: effs.append({'type':'Exile','name':cname,'tagToCheck':mf.get('tagToCheck','0')})
                        elif 'CardManipulationEffect' in cid: effs.append({'type':'Manip','name':cname,'tagToCheck':mf.get('tagToCheck','0')})
                        else: effs.append({'type':'Bury','name':cname,'tagToCheck':mf.get('tagToCheck','0')})
                    elif 'cardCount' in mf and 'curseCardTypeID' in mf:
                        effs.append({'type':'AddTemp','name':cname,'cardCount':mf.get('cardCount','0'),'curseCardTypeID':mf.get('curseCardTypeID','')})
                    elif 'powerCoefficient' in mf and 'cardTypeID' in mf:
                        effs.append({'type':'Curse','name':cname,'cardTypeID':mf.get('cardTypeID',''),'powerCoefficient':mf.get('powerCoefficient','0')})
                    elif 'statusEffectToTransfer' in mf:
                        effs.append({'type':'Transfer','name':cname,'isFromFriendly':mf.get('isFromFriendly','0'),'statusEffectToTransfer':mf.get('statusEffectToTransfer','0'),'curseCardTypeID':mf.get('curseCardTypeID','')})
                    elif 'statusEffectMultiplier' in mf:
                        effs.append({'type':'Amplifier','name':cname,'statusEffectToGive':mf.get('statusEffectToGive','0'),'statusEffectToCount':mf.get('statusEffectToCount','0'),'statusEffectMultiplier':mf.get('statusEffectMultiplier','0'),'target':mf.get('target','0'),'includeSelf':mf.get('includeSelf','0'),'lastXCardsCount':mf.get('lastXCardsCount','0'),'xFriendlyCount':mf.get('xFriendlyCount','0'),'statusEffectLayerCount':mf.get('statusEffectLayerCount','0'),'yFriendlyLayerCount':mf.get('yFriendlyLayerCount','0')})
                    elif 'powerAmount' in mf:
                        effs.append({'type':'PowerReaction','name':cname,'powerAmount':mf.get('powerAmount','0'),'excludeSelf':mf.get('excludeSelf','0'),'statusEffectToGive':mf.get('statusEffectToGive','0'),'statusEffectToCount':mf.get('statusEffectToCount','0'),'target':mf.get('target','0'),'includeSelf':mf.get('includeSelf','0'),'lastXCardsCount':mf.get('lastXCardsCount','0'),'xFriendlyCount':mf.get('xFriendlyCount','0'),'statusEffectLayerCount':mf.get('statusEffectLayerCount','0'),'yFriendlyLayerCount':mf.get('yFriendlyLayerCount','0')})
                    elif 'statusEffectToGive' in mf:
                        effs.append({'type':'Giver','name':cname,'statusEffectToGive':mf.get('statusEffectToGive','0'),'statusEffectToCount':mf.get('statusEffectToCount','0'),'target':mf.get('target','0'),'includeSelf':mf.get('includeSelf','0'),'lastXCardsCount':mf.get('lastXCardsCount','0'),'xFriendlyCount':mf.get('xFriendlyCount','0'),'statusEffectLayerCount':mf.get('statusEffectLayerCount','0'),'yFriendlyLayerCount':mf.get('yFriendlyLayerCount','0')})
                    elif 'statusEffectToConsume' in mf:
                        effs.append({'type':'Consumer','name':cname,'statusEffectToConsume':mf.get('statusEffectToConsume','0')})
                    elif 'm_EditorClassIdentifier' in mf:
                        cid=mf.get('m_EditorClassIdentifier','')
                        if 'ShieldAlterEffect' in cid: effs.append({'type':'Shield','name':cname})
                        elif 'ChangeCardTarget' in cid: effs.append({'type':'ChangeTarget','name':cname})
                        elif 'ChangeHpAlterAmountEffect' in cid: effs.append({'type':'ChangeHpAlter','name':cname})
                        elif 'HPMaxAlterEffect' in cid: effs.append({'type':'HPMaxAlter','name':cname})
    return {
        'prefab_name':rgo['name'],'path':path,
        'cardTypeID':cs.get('cardTypeID',''),
        'displayName':cs.get('displayName','').strip('"'),
        'cardDesc':cs.get('cardDesc','').strip('"').replace('\\n','\\n'),
        'isMinion':cs.get('isMinion','0')=='1',
        'buryCost':cs.get('buryCost','0'),'delayCost':cs.get('delayCost','0'),'exposeCost':cs.get('exposeCost','0'),
        'minionCostCount':cs.get('minionCostCount','0'),'minionCostCardTypeID':cs.get('minionCostCardTypeID',''),
        'minionCostOwner':cs.get('minionCostOwner','0'),
        'statusEffects':sl,'tags':tl,'containers':cons,'effects':effs
    }

def build_guid_map():
    gmap={}
    for root, dirs, files in os.walk(EVENTS):
        for f in files:
            if f.endswith('.asset.meta'):
                meta_path=os.path.join(root,f)
                with open(meta_path,'r',encoding='utf-8') as fi:
                    for line in fi:
                        m=re.search(r'^guid:\s*([a-f0-9]+)',line)
                        if m:
                            # event name from filename
                            name=f.replace('.asset.meta','')
                            gmap[m.group(1)]=name
                            break
            elif f.endswith('.asset') and not os.path.exists(os.path.join(root,f+'.meta')):
                pass
    return gmap

def fmt_calls(clist):
    if not clist: return ''
    parts=[]
    for c in clist:
        s=c['target_type']+'->'+c['method']+'('+str(c['int_arg'])+','+c['string_arg']+')'
        parts.append(s)
    return ', '.join(parts)

def fmt_eff(e):
    t=e['type']
    if t=='HPAlter':
        se=SE_NAMES[int(e.get('statusEffectToCheck','0'))] if int(e.get('statusEffectToCheck','0'))<len(SE_NAMES) else e.get('statusEffectToCheck','0')
        return 'HPAlter: baseDmg='+e.get('baseDmg','0')+' extraDmg='+e.get('extraDmg','0')+' statusEffect='+se
    if t=='Stage':
        se=SE_NAMES[int(e.get('statusEffectToCheck','0'))] if int(e.get('statusEffectToCheck','0'))<len(SE_NAMES) else e.get('statusEffectToCheck','0')
        return 'Stage: tag='+TAG_NAMES[int(e.get('tagToCheck','0'))] if int(e.get('tagToCheck','0'))<len(TAG_NAMES) else 'Stage'
    if t=='Bury':
        return 'Bury'+((': tag='+TAG_NAMES[int(e.get('tagToCheck','0'))]) if int(e.get('tagToCheck','0'))>0 and int(e.get('tagToCheck','0'))<len(TAG_NAMES) else '')
    if t=='Exile': return 'Exile'
    if t=='Manip': return 'Manip'
    if t=='AddTemp': return 'AddTemp: cardCount='+e.get('cardCount','0')
    if t=='Curse': return 'Curse: powerCoefficient='+e.get('powerCoefficient','0')
    if t=='Transfer':
        se=SE_NAMES[int(e.get('statusEffectToTransfer','0'))] if int(e.get('statusEffectToTransfer','0'))<len(SE_NAMES) else e.get('statusEffectToTransfer','0')
        return 'Transfer: fromFriendly='+e.get('isFromFriendly','0')+' effect='+se
    if t=='Amplifier': return 'Amplifier: multiplier='+e.get('statusEffectMultiplier','0')
    if t=='PowerReaction': return 'PowerReaction: powerAmount='+e.get('powerAmount','0')
    if t=='Giver':
        return 'Giver: give='+SE_NAMES[int(e.get('statusEffectToGive','0'))]+' count='+SE_NAMES[int(e.get('statusEffectToCount','0')) if e.get('statusEffectToCount','0') else 'None']+(' lastXCards='+e.get('lastXCardsCount','0') if e.get('lastXCardsCount','0')!='0' else '')+(' xFriendly='+e.get('xFriendlyCount','0') if e.get('xFriendlyCount','0')!='0' else '')+(' layerCount='+e.get('statusEffectLayerCount','0')+', yLayerCount='+e.get('yFriendlyLayerCount','0') if e.get('statusEffectLayerCount','0')!='0' or e.get('yFriendlyLayerCount','0')!='0' else '')
    if t=='Consumer': return 'Consumer: consume='+SE_NAMES[int(e.get('statusEffectToConsume','0'))]
    if t=='Shield': return 'Shield'
    if t=='ChangeTarget': return 'ChangeTarget'
    if t=='ChangeHpAlter': return 'ChangeHpAlter'
    if t=='HPMaxAlter': return 'HPMaxAlter'
    return t

def generate_card_design(cards, gmap):
    lines=[]
    lines.append('# OneDeck 3.0 No Cost Card Design Document')
    lines.append('')
    lines.append('> This document is auto-generated from prefab data under `Assets/Prefabs/Cards/3.0 no cost (current)`.')
    lines.append('> Generation date: '+datetime.now().strftime('%Y-%m-%d %H:%M'))
    lines.append('')
    lines.append('---')
    lines.append('')
    lines.append('## Table of Contents')
    lines.append('')
    lines.append('- [Overview](#overview)')
    lines.append('- [Glossary](#glossary)')
    cats=sorted(set(c['category'] for c in cards))
    for cat in cats:
        lines.append('- ['+cat+'](#'+cat.lower().replace(' ','-').replace('&','and')+')')
    lines.append('')
    lines.append('---')
    lines.append('')
    lines.append('## Overview')
    lines.append('')
    lines.append('| Category | Count |')
    lines.append('|----------|-------|')
    for cat in cats:
        cnt=sum(1 for c in cards if c['category']==cat)
        lines.append('| '+cat+' | '+str(cnt)+' |')
    lines.append('| **Total** | **'+str(len(cards))+'** |')
    lines.append('')
    lines.append('---')
    lines.append('')
    lines.append('## Glossary')
    lines.append('')
    lines.append('| Term | Description |')
    lines.append('|------|-------------|')
    lines.append('| Bury | Move card to the bottom of the deck |')
    lines.append('| Stage | Move card to the top of the deck |')
    lines.append('| Exile | Remove card from the game |')
    lines.append('| Linger | Card can trigger effects while positioned before the Start Card in deck |')
    lines.append('| DeathRattle | Effect triggers when the card is buried |')
    lines.append('| Power | Status effect; each stack increases damage by 1 |')
    lines.append('| Minion Cost | Consume N friendly Minion cards to activate the effect |')
    lines.append('')
    for cat in cats:
        lines.append('## '+cat)
        lines.append('')
        for c in cards:
            if c['category']!=cat: continue
            lines.append('### '+c['displayName']+' (`'+c['cardTypeID']+'`)')
            lines.append('')
            lines.append('| Field | Value |')
            lines.append('|-------|-------|')
            flags=[]
            if c['isMinion']: flags.append('Minion')
            if c['tags']:
                flags.append('Tags='+','.join(TAG_NAMES[t] for t in c['tags'] if 0<=t<len(TAG_NAMES)))
            if c['statusEffects']:
                flags.append('Status='+','.join(SE_NAMES[s] for s in c['statusEffects'] if 0<=s<len(SE_NAMES)))
            costs=[]
            if c['buryCost']!='0': costs.append('Bury='+c['buryCost'])
            if c['delayCost']!='0': costs.append('Delay='+c['delayCost'])
            if c['exposeCost']!='0': costs.append('Expose='+c['exposeCost'])
            if c['minionCostCount']!='0': costs.append('Minion='+c['minionCostCount']+'('+c['minionCostCardTypeID']+'/'+TARGET_TYPE_NAMES[int(c['minionCostOwner'])] +')')
            lines.append('| Name | `'+c['displayName']+'` (`'+c['cardTypeID']+'`) |')
            lines.append('| Flags | '+(' / '.join(flags) if flags else 'None')+' |')
            lines.append('| Costs | '+(' / '.join(costs) if costs else 'None')+' |')
            cd=c['cardDesc'].replace('\\n','<br>')
            lines.append('| Desc | '+cd+' |')
            cont_strs=[]
            for con in c['containers']:
                trig=gmap.get(con['trigger_guid'],'NONE')
                s='- **'+con['name']+'** | Trigger:`'+trig+'`'
                if con['check_cost']: s+=' | Check: '+fmt_calls(con['check_cost'])
                if con['pre_effect']: s+=' | Pre: '+fmt_calls(con['pre_effect'])
                if con['effect']: s+=' | Effect: '+fmt_calls(con['effect'])
                cont_strs.append(s)
            lines.append('| Containers | '+'<br>'.join(cont_strs)+' |')
            eff_strs=[fmt_eff(e) for e in c['effects']]
            lines.append('| Key Fields | '+('<br>'.join(eff_strs) if eff_strs else '')+' |')
        lines.append('')
    lines.append('---')
    lines.append('')
    lines.append('> End of document')
    with open(os.path.join(DOCS,'3.0_no_cost_CardDesign.md'),'w',encoding='utf-8') as f:
        f.write('\n'.join(lines))

def generate_test_plan(cards, gmap):
    lines=[]
    lines.append('# OneDeck 3.0 No Cost Test Plan')
    lines.append('')
    lines.append('> This document contains test plans for all cards under `Assets/Prefabs/Cards/3.0 no cost (current)`.')
    lines.append('> Generation date: '+datetime.now().strftime('%Y-%m-%d %H:%M'))
    lines.append('')
    lines.append('---')
    lines.append('')
    cats=sorted(set(c['category'] for c in cards))
    for cat in cats:
        lines.append('## '+cat)
        lines.append('')
        for c in cards:
            if c['category']!=cat: continue
            lines.append('### '+c['displayName']+' (`'+c['cardTypeID']+'`)')
            lines.append('')
            lines.append('**Prefab:** `'+c['path'].replace('d:\\Unity Projects\\OneDeck\\','').replace('\\','/')+'`')
            lines.append('')
            lines.append('| Property | Value |')
            lines.append('|----------|-------|')
            lines.append('| **Is Minion** | '+('Yes' if c['isMinion'] else 'No')+' |')
            flags=[]
            if c['tags']:
                flags.append('Tags: '+','.join(TAG_NAMES[t] for t in c['tags'] if 0<=t<len(TAG_NAMES)))
            if c['statusEffects']:
                flags.append('Status: '+','.join(SE_NAMES[s] for s in c['statusEffects'] if 0<=s<len(SE_NAMES)))
            lines.append('| **Flags** | '+(' / '.join(flags) if flags else 'None')+' |')
            costs=[]
            if c['buryCost']!='0': costs.append('Bury='+c['buryCost'])
            if c['delayCost']!='0': costs.append('Delay='+c['delayCost'])
            if c['exposeCost']!='0': costs.append('Expose='+c['exposeCost'])
            if c['minionCostCount']!='0': costs.append('Minion='+c['minionCostCount']+'('+c['minionCostCardTypeID']+')')
            lines.append('| **Costs** | '+(' / '.join(costs) if costs else 'None')+' |')
            lines.append('')
            lines.append('#### Implementation Chain')
            for con in c['containers']:
                trig=gmap.get(con['trigger_guid'],'NONE')
                lines.append('1. **Trigger:** `'+trig+'` -> `'+con['name']+'`')
                if con['check_cost']:
                    lines.append('   - **Cost Check:** '+fmt_calls(con['check_cost']))
                if con['pre_effect']:
                    lines.append('   - **Pre-Effect:** '+fmt_calls(con['pre_effect']))
                if con['effect']:
                    lines.append('   - **Effect:** '+fmt_calls(con['effect']))
            lines.append('')
            lines.append('#### Key Effect Fields')
            for e in c['effects']:
                lines.append('- '+fmt_eff(e))
            lines.append('')
            lines.append('#### Test Cases')
            lines.append('')
            lines.append('| ID | Scenario | Deck Setup | Expected Result | Validation Point |')
            lines.append('|----|----------|------------|-----------------|------------------|')
            tc=1
            # Base cases
            for con in c['containers']:
                trig=gmap.get(con['trigger_guid'],'NONE')
                eff_str=fmt_calls(con['effect'])
                if 'HPAlter' in eff_str:
                    # Determine base damage
                    dmg='?'
                    for e in c['effects']:
                        if e['type']=='HPAlter':
                            dmg=str(int(e['baseDmg'])+int(e['extraDmg']))
                            break
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: deal damage | Deck contains card | Enemy HP -'+dmg+' | Damage number correct |')
                    tc+=1
                if 'Bury' in eff_str and 'BuryMyCards' in eff_str:
                    n='1'
                    for call in con['effect']:
                        if 'BuryMyCards' in call['method']:
                            n=str(call['int_arg'])
                            break
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: bury friendly | Deck has friendly cards | '+n+' friendly card(s) moved to bottom | Bury target correct |')
                    tc+=1
                if 'Bury' in eff_str and 'BuryTheirCards' in eff_str:
                    n='1'
                    for call in con['effect']:
                        if 'BuryTheirCards' in call['method']:
                            n=str(call['int_arg'])
                            break
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: bury enemy | Deck has enemy cards | '+n+' enemy card(s) moved to bottom | Bury target correct |')
                    tc+=1
                if 'Stage' in eff_str and 'StageMyCards' in eff_str:
                    n='1'
                    for call in con['effect']:
                        if 'StageMyCards' in call['method']:
                            n=str(call['int_arg'])
                            break
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: stage friendly | Deck has friendly cards | '+n+' friendly card(s) moved to top | Stage target correct |')
                    tc+=1
                if 'Giver' in eff_str or 'PowerReaction' in eff_str or 'Amplifier' in eff_str:
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: status effect applied | Target cards in deck | Correct cards gain status | Target selection correct |')
                    tc+=1
                if 'Curse' in eff_str:
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: enhance curse | Enemy has curse card | Curse power increased | Enhancement amount correct |')
                    tc+=1
                if 'AddTemp' in eff_str:
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: add temp card | - | Temp card added to deck | Card count increased |')
                    tc+=1
                if 'Transfer' in eff_str:
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: transfer status | Source cards have status | Status moved to target | Transfer count correct |')
                    tc+=1
                if 'Exile' in eff_str:
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: exile card | Target card in deck | Card removed from game | Exile target correct |')
                    tc+=1
                if 'Shield' in eff_str:
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Reveal: gain shield | - | Player shield increased | Shield amount correct |')
                    tc+=1
                if 'Consumer' in eff_str or 'CheckCost' in str(con.get('check_cost','')):
                    lines.append('| '+c['cardTypeID']+'-'+str(tc)+' | Cost failure: insufficient resource | Cost condition not met | Effect does not trigger | Graceful skip |')
                    tc+=1
            if tc==1:
                lines.append('| '+c['cardTypeID']+'-1 | Basic reveal | Deck contains card | Effect triggers as described | Behavior matches description |')
            lines.append('')
        lines.append('---')
        lines.append('')
    with open(os.path.join(DOCS,'3.0_no_cost_TestPlan.md'),'w',encoding='utf-8') as f:
        f.write('\n'.join(lines))

def main():
    gmap=build_guid_map()
    prefabs=glob.glob(os.path.join(CARDS,'**','*.prefab'),recursive=True)
    prefabs.sort()
    cards=[]
    for p in prefabs:
        card=parse(p)
        if not card: 
            print('WARN: failed to parse '+p)
            continue
        # Determine category from path
        rel=os.path.relpath(p,CARDS)
        parts=rel.split(os.sep)
        cat=parts[0] if parts else 'Unknown'
        card['category']=cat
        cards.append(card)
    cards.sort(key=lambda x:(x['category'],x['cardTypeID']))
    generate_card_design(cards,gmap)
    generate_test_plan(cards,gmap)
    print('Generated '+str(len(cards))+' cards')
    print('Design doc: docs/3.0_no_cost_CardDesign.md')
    print('Test plan: docs/3.0_no_cost_TestPlan.md')

if __name__=='__main__':
    main()
