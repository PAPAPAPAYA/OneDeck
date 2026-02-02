import os
import re
from collections import defaultdict

card_dir = 'Assets/Prefabs/Cards'
card_files = []
for root, dirs, files in os.walk(card_dir):
    for f in files:
        if f.endswith('.prefab'):
            card_files.append(os.path.join(root, f))

print(f'找到 {len(card_files)} 个卡牌预制体')

# 定义机制分析器
class MechanicAnalyzer:
    def __init__(self):
        # 效果类型统计
        self.effect_types = defaultdict(lambda: {'count': 0, 'cards': set()})
        # 目标类型统计  
        self.target_types = defaultdict(lambda: {'count': 0, 'cards': set()})
        # 费用/条件类型统计
        self.cost_types = defaultdict(lambda: {'count': 0, 'cards': set()})
        # 特殊机制统计
        self.special_mechanics = defaultdict(lambda: {'count': 0, 'cards': set()})
        # 卡牌类别统计（基于文件夹）
        self.card_categories = defaultdict(lambda: {'count': 0, 'cards': []})
        
    def analyze_card(self, filepath, content):
        filename = os.path.basename(filepath)
        category = os.path.basename(os.path.dirname(filepath))
        
        # 跳过模板
        if 'Template' in filename:
            return
            
        self.card_categories[category]['count'] += 1
        self.card_categories[category]['cards'].append(filename)
        
        # 分析效果类型
        self._analyze_effects(content, filename)
        
        # 分析目标类型
        self._analyze_targets(content, filename)
        
        # 分析费用/条件
        self._analyze_costs(content, filename)
        
        # 分析特殊机制
        self._analyze_special_mechanics(content, filename)
        
    def _analyze_effects(self, content, filename):
        # 效果脚本类型映射
        effect_patterns = {
            r'HPAlterEffect': '伤害/治疗(HP)',
            r'ShieldAlter': '护盾(Shield)',
            r'CardManipulationEffect': '卡牌操控',
            r'ChangeCardTarget': '改变目标(HeartChange)',
            r'ChangeHpAlterAmountEffect': '伤害/治疗量修改',
            r'HpMaxAlterEffect': '最大生命值修改',
            r'AddTempCard': '生成临时卡牌',
            r'PrintEffect': '调试打印',
            r'InfectionEffect': '感染(Infection)',
            r'ManaAlterEffect': '法力(Mana)',
            r'HeartChangeEffect': '心脏变换(HeartChange)',
            r'GivePowerStatusEffectEffect': '力量增益(Power)',
            r'ConsumeStausEffect': '消耗状态',
            r'StatusEffectGiverEffect': '状态赋予',
            r'DeckSizeIncreaseEffect': '卡组容量增加',
        }
        
        for pattern, name in effect_patterns.items():
            if re.search(pattern, content):
                self.effect_types[name]['count'] += 1
                self.effect_types[name]['cards'].add(filename)
    
    def _analyze_targets(self, content, filename):
        # 分析方法调用来确定目标
        target_patterns = {
            r'DecreaseTheirHp': '对敌人造成伤害',
            r'DecreaseMyHp': '对自己造成伤害',
            r'IncreaseTheirHp': '治疗敌人',
            r'IncreaseMyHp': '治疗自己',
            r'UpMyShield': '给自己护盾',
        }
        
        for pattern, name in target_patterns.items():
            matches = re.findall(pattern, content)
            if matches:
                self.target_types[name]['count'] += len(matches)
                self.target_types[name]['cards'].add(filename)
    
    def _analyze_costs(self, content, filename):
        # 费用检查类型
        cost_patterns = {
            r'CheckCost_Infected\(\)': '感染条件',
            r'CheckCost_Mana\((\d+)\)': '法力消耗',
            r'CheckCost_InGrave\(\)': '墓地条件',
            r'CheckCost_Revive\((\d+)\)': '复活消耗',
            r'CheckCost_Rested\((\d+)\)': '休息消耗',
        }
        
        for pattern, name in cost_patterns.items():
            matches = re.findall(pattern, content)
            if matches:
                self.cost_types[name]['count'] += len(matches)
                self.cost_types[name]['cards'].add(filename)
    
    def _analyze_special_mechanics(self, content, filename):
        # 特殊机制检测
        mechanic_patterns = {
            r'StageCard|stage': 'Staging(暂存卡牌)',
            r'BuryCard|bury': 'Bury(埋卡)',
            r'Grave|graveyard': '墓地互动',
            r'Revive|revive': '复活机制',
            r'shiv|Shiv': 'Shiv(小刀)',
            r'linger|Linger': '延迟触发',
            r'curse|Curse': '诅咒',
            r'power|Power': '力量机制',
            r'infection|Infection': '感染机制',
            r'mana|Mana': '法力机制',
            r'takeUpSpace:\s*0': '临时卡牌(不占空间)',
        }
        
        for pattern, name in mechanic_patterns.items():
            if re.search(pattern, content, re.IGNORECASE):
                self.special_mechanics[name]['count'] += 1
                self.special_mechanics[name]['cards'].add(filename)
                
    def print_report(self):
        print('=' * 60)
        print('                  卡牌机制统计报告')
        print('=' * 60)
        
        print('\n【一、卡牌类别分布】')
        print('-' * 60)
        for cat, data in sorted(self.card_categories.items(), key=lambda x: -x[1]['count']):
            print(f'{cat}: {data["count"]} 张')
        
        print('\n【二、效果组件类型】')
        print('-' * 60)
        for eff, data in sorted(self.effect_types.items(), key=lambda x: -x[1]['count']):
            if data['count'] > 0:
                print(f'{eff}: {data["count"]} 次 (涉及 {len(data["cards"])} 张卡牌)')
        
        print('\n【三、目标与作用类型】')
        print('-' * 60)
        for tgt, data in sorted(self.target_types.items(), key=lambda x: -x[1]['count']):
            print(f'{tgt}: {data["count"]} 次 (涉及 {len(data["cards"])} 张卡牌)')
        
        print('\n【四、费用/条件类型】')
        print('-' * 60)
        for cost, data in sorted(self.cost_types.items(), key=lambda x: -x[1]['count']):
            print(f'{cost}: {data["count"]} 次 (涉及 {len(data["cards"])} 张卡牌)')
        
        print('\n【五、特殊机制】')
        print('-' * 60)
        for mech, data in sorted(self.special_mechanics.items(), key=lambda x: -x[1]['count']):
            print(f'{mech}: {data["count"]} 次 (涉及 {len(data["cards"])} 张卡牌)')
        
        print('\n' + '=' * 60)
        print(f'总计分析卡牌: {len(self.card_categories)} 个类别, {sum(c["count"] for c in self.card_categories.values())} 张卡牌')
        print('=' * 60)

# 执行分析
analyzer = MechanicAnalyzer()

for filepath in card_files:
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        analyzer.analyze_card(filepath, content)
    except Exception as e:
        print(f'Error reading {filepath}: {e}')

analyzer.print_report()
