#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
通用 Google Fonts 下载器
使用方法：
  python font_downloader.py [CSS_URL] [options]
  
示例：
  python font_downloader.py "https://fonts.googleapis.com/css2?family=Comfortaa:wght@300;400;500;600;700&display=swap"
  python font_downloader.py --download-all  # 下载项目所需的所有字体
"""

import os
import re
import sys
import argparse
import requests
from urllib.parse import urlparse, unquote
from pathlib import Path

# 设置输出编码为UTF-8
import codecs
if sys.platform.startswith('win'):
    sys.stdout = codecs.getwriter('utf-8')(sys.stdout.buffer, 'strict')
    sys.stderr = codecs.getwriter('utf-8')(sys.stderr.buffer, 'strict')

class UniversalFontDownloader:
    def __init__(self):
        self.session = requests.Session()
        self.session.headers.update({
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'
        })
        
        # 项目所需的字体URLs
        self.project_fonts = [
            'https://fonts.googleapis.com/css2?family=Noto+Serif+SC:wght@300;400;500;600;700&display=swap',
            'https://fonts.googleapis.com/css2?family=Comfortaa:wght@300;400;500;600;700&display=swap',
            'https://fonts.googleapis.com/css2?family=Nunito:wght@300;400;500;600;700&display=swap',
            'https://fonts.googleapis.com/css2?family=Source+Sans+Pro:wght@300;400;500;600;700&display=swap',
            'https://fonts.googleapis.com/css2?family=Open+Sans:wght@300;400;500;600;700&display=swap',
            'https://fonts.googleapis.com/css2?family=Courier+Prime:wght@400;700&display=swap',
            'https://fonts.googleapis.com/css2?family=Playfair+Display:wght@400;500;600;700&display=swap',
            'https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;500;600&display=swap',
            'https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap'
        ]

    def download_css(self, url):
        """下载CSS文件并返回内容"""
        try:
            print(f"[INFO] 获取CSS: {url}")
            response = self.session.get(url, timeout=30)
            response.raise_for_status()
            return response.text
        except Exception as e:
            print(f"[ERROR] 下载CSS失败: {e}")
            return None

    def extract_font_info_from_css(self, css_content):
        """从CSS内容中提取字体信息"""
        fonts = []
        
        # 匹配 @font-face 规则
        font_face_pattern = r'@font-face\s*\{([^}]+)\}'
        
        for match in re.finditer(font_face_pattern, css_content, re.DOTALL):
            font_face_content = match.group(1)
            
            # 提取字体家族名
            family_match = re.search(r'font-family:\s*[\'"]([^\'"]+)[\'"]', font_face_content)
            if not family_match:
                continue
            family_name = family_match.group(1)
            
            # 提取字重
            weight_match = re.search(r'font-weight:\s*(\d+)', font_face_content)
            weight = weight_match.group(1) if weight_match else '400'
            
            # 提取字体样式
            style_match = re.search(r'font-style:\s*(\w+)', font_face_content)
            style = style_match.group(1) if style_match else 'normal'
            
            # 提取woff2 URL
            url_match = re.search(r'url\((https://[^)]+\.woff2)\)', font_face_content)
            if not url_match:
                continue
            font_url = url_match.group(1)
            
            # 生成文件名
            style_suffix = '' if style == 'normal' else f'-{style.capitalize()}'
            weight_name = self.get_weight_name(weight)
            filename = f"{family_name.replace(' ', '')}-{weight_name}{style_suffix}.woff2"
            
            fonts.append({
                'family': family_name,
                'weight': weight,
                'style': style,
                'url': font_url,
                'filename': filename
            })
        
        return fonts

    def get_weight_name(self, weight):
        """将数字字重转换为名称"""
        weight_map = {
            '100': 'Thin',
            '200': 'ExtraLight', 
            '300': 'Light',
            '400': 'Regular',
            '500': 'Medium',
            '600': 'SemiBold',
            '700': 'Bold',
            '800': 'ExtraBold',
            '900': 'Black'
        }
        return weight_map.get(weight, f'W{weight}')

    def download_font_file(self, font_info, output_dir='.'):
        """下载单个字体文件"""
        try:
            filename = os.path.join(output_dir, font_info['filename'])
            
            # 如果文件已存在，跳过
            if os.path.exists(filename):
                print(f"  [SKIP]  已存在: {font_info['filename']}")
                return True
            
            print(f"  [INFO] 下载: {font_info['filename']}")
            response = self.session.get(font_info['url'], timeout=60)
            response.raise_for_status()
            
            os.makedirs(output_dir, exist_ok=True)
            with open(filename, 'wb') as f:
                f.write(response.content)
            
            file_size = len(response.content) / 1024
            print(f"  [OK] 完成: {font_info['filename']} ({file_size:.1f} KB)")
            return True
            
        except Exception as e:
            print(f"  [ERROR] 下载失败 {font_info['filename']}: {e}")
            return False

    def process_css_url(self, css_url, output_dir='.', filter_weights=None):
        """处理单个CSS URL"""
        print(f"\n[PROCESS] 处理字体: {css_url}")
        
        # 下载CSS
        css_content = self.download_css(css_url)
        if not css_content:
            return False
        
        # 提取字体信息
        fonts = self.extract_font_info_from_css(css_content)
        if not fonts:
            print("  [WARN]  未找到字体文件")
            return False
        
        # 按字体家族分组
        font_families = {}
        for font in fonts:
            family = font['family']
            if family not in font_families:
                font_families[family] = []
            font_families[family].append(font)
        
        print(f"  [INFO] 找到 {len(font_families)} 个字体家族，共 {len(fonts)} 个文件")
        
        # 下载字体文件
        success_count = 0
        for family_name, family_fonts in font_families.items():
            print(f"\n  [FAMILY] 字体家族: {family_name}")
            
            for font_info in family_fonts:
                # 如果指定了字重过滤器，只下载指定字重
                if filter_weights and font_info['weight'] not in filter_weights:
                    continue
                    
                if self.download_font_file(font_info, output_dir):
                    success_count += 1
        
        print(f"\n  [SUMMARY] 成功下载: {success_count} 个文件")
        return success_count > 0

    def download_project_fonts(self, output_dir='.'):
        """下载项目所需的所有字体"""
        print("[START] 下载项目所需的所有字体...")
        print("="*60)
        
        # 只下载常用字重 (Regular, Medium, SemiBold)
        common_weights = ['400', '500', '600']
        
        successful = 0
        for css_url in self.project_fonts:
            if self.process_css_url(css_url, output_dir, filter_weights=common_weights):
                successful += 1
        
        print(f"\n[COMPLETE] 完成！成功处理: {successful}/{len(self.project_fonts)} 个字体")
        return successful > 0

def main():
    parser = argparse.ArgumentParser(
        description='通用 Google Fonts 下载器',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
使用示例:
  %(prog)s "https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500&display=swap"
  %(prog)s --download-all
  %(prog)s "https://fonts.googleapis.com/css2?family=Comfortaa:wght@400&display=swap" -o ./fonts
        """
    )
    
    parser.add_argument('css_url', nargs='?', help='Google Fonts CSS URL')
    parser.add_argument('--download-all', action='store_true', help='下载项目所需的所有字体')
    parser.add_argument('-o', '--output', default='.', help='输出目录 (默认: 当前目录)')
    parser.add_argument('--weights', help='指定要下载的字重，用逗号分隔 (如: 400,500,600)')
    
    args = parser.parse_args()
    
    if not args.css_url and not args.download_all:
        parser.print_help()
        sys.exit(1)
    
    try:
        downloader = UniversalFontDownloader()
        
        # 解析字重过滤器
        filter_weights = None
        if args.weights:
            filter_weights = [w.strip() for w in args.weights.split(',')]
        
        if args.download_all:
            # 下载项目所需的所有字体
            success = downloader.download_project_fonts(args.output)
        else:
            # 下载指定URL的字体
            success = downloader.process_css_url(args.css_url, args.output, filter_weights)
        
        if success:
            print(f"\n[SUCCESS] 下载完成！字体文件保存在: {os.path.abspath(args.output)}")
        else:
            print(f"\n[ERROR] 下载失败！")
            sys.exit(1)
            
    except KeyboardInterrupt:
        print("\n\n[INTERRUPTED]  下载被用户中断")
        sys.exit(1)
    except Exception as e:
        print(f"\n[ERROR] 发生错误: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()