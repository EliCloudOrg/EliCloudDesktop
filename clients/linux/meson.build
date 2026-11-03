project(
  'elicloud-hello',
  version: '0.1.0',
  meson_version: '>=1.3.0',
  default_options: ['warning_level=2'],
)

py = import('python').find_installation('python3', required: true)

# Python 工程的“build”就是安装布局 + 字节码编译；GTK 依赖只在运行时需要。
py.install_sources(
  files('src/elicloud/__init__.py', 'src/elicloud/__main__.py', 'src/elicloud/hello.py'),
  subdir: 'elicloud',
)
py.install_sources(
  files('src/elicloud/design/__init__.py'),
  subdir: 'elicloud/design',
)
# tokens.py / config.py / copy.py 是生成物（tokens/copy 入库，config 不入库）。
py.install_sources(
  files(
    'src/elicloud/design/tokens.py',
    'src/elicloud/copy.py',
    'src/elicloud/config.py',
  ),
  subdir: 'elicloud',
)

py.compile_sources()

test(
  'generated-artifacts',
  py,
  args: [files('tests/test_generated.py')],
  env: ['PYTHONPATH=' + join_paths(meson.project_source_root(), 'src')],
  timeout: 30,
)

summary({'python': py.full_path()}, section: 'runtime')
