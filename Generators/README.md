# Generators

## 解决什么问题
边界、路线和热力图是地理编辑器里高频但可独立的几何生成任务。

## 实现思路
各工具只保留几何和 shadergraph 路线，去掉真实业务数据和二进制插件。

## 基本用法
用合成点列调用 BorderGenerator、RouteGenerator 或 HeatMapGenerator。

## 依赖
无特殊依赖；按根 README 安装 Unity/UE 常规依赖即可。
