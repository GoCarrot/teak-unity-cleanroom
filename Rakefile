require "rake/clean"
require "shellwords"
require "mustache"
CLEAN.include "**/.DS_Store"

#
# Extend Rake to have current_task
#
require 'rake'
module Rake
  class Application
    attr_accessor :current_task
  end
  class Task
    alias :old_execute :execute
    def execute(args=nil)
      Rake.application.current_task = self
      old_execute(args)
    end
  end #class Task
end #module Rake

desc "Build Unity package"
task :default

CIRCLE_ARTIFACTS = ENV.fetch('CIRCLE_ARTIFACTS', nil)
UNITY_HOME = ENV.fetch('UNITY_HOME', '/Applications/Unity')
RVM_VARS = %w(GEM_HOME IRBRC MY_RUBY_HOME GEM_PATH)
PROJECT_PATH = Rake.application.original_dir
BUILD_TYPE = ENV.fetch('BUILD_TYPE', 'dev')
TARGET_API = ENV.fetch('TARGET_API', 25)
TEAK_CREDENTIALS = {
  'dev' => {
    package_name: 'io.teak.app.unity.dev',
    teak_app_id: '613659812345256',
    teak_api_key: '41ff00cfd4cb85702e265aa3d5ab7858',
    teak_gcm_sender_id: '944348058057',
    teak_short_url_domain: 'teak-dev.playw.it'
  },
  'prod' => {
    package_name: 'io.teak.app.unity.prod',
    teak_app_id: '1136371193060244',
    teak_api_key: '1f3850f794b9093864a0778009744d03',
    teak_gcm_sender_id: '944348058057',
    teak_short_url_domain: 'teak-prod.playw.it'
  }
}
PACKAGE_NAME = TEAK_CREDENTIALS[BUILD_TYPE][:package_name]
TEAK_SDK_VERSION = ENV.fetch('TEAK_SDK_VERSION', nil) ? "-#{ENV.fetch('TEAK_SDK_VERSION')}" : ""

def unity?
  File.exist? "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity"
end

def ci?
  ENV.fetch('CI', false).to_s == 'true'
end

#
# Template parameters
#
def template_parameters
  teak_version = File.read(File.join(PROJECT_PATH, 'Assets', 'Teak', 'TeakVersion.cs')).match(/return "(.*)"/).captures[0]
  TEAK_CREDENTIALS[BUILD_TYPE].merge({
    app_name: "#{BUILD_TYPE.capitalize} #{teak_version}",
    target_api: TARGET_API
  })
end

#
# Play a sound after finished
#
at_exit do
  sh "afplay /System/Library/Sounds/Submarine.aiff" unless ci?
  begin
    sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -batchmode -quit -returnlicense" if ci?
  end
end

#
# Helper methods
#
def xcodebuild(*args)
  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "xcodebuild #{escaped_args} | xcpretty"
end

def unity(*args, quit: true, nographics: true)
  args.push("-serial", ENV["UNITY_SERIAL"], "-username", ENV["UNITY_EMAIL"], "-password", ENV["UNITY_PASSWORD"]) if ci?

  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -logFile #{PROJECT_PATH}/unity.log#{quit ? ' -quit' : ''}#{nographics ? ' -nographics' : ''} -batchmode -projectPath #{PROJECT_PATH} #{escaped_args}"
  ensure
    return unless CIRCLE_ARTIFACTS
    cp('unity.log', File.join(CIRCLE_ARTIFACTS, "#{Rake.application.current_task.name.sub(':', '-')}.unity.log")) unless $!.nil?
end

def fastlane(*args, env:{})
  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "#{env.map{|k,v| "#{k}='#{v}'"}.join(' ')} bundle exec fastlane #{escaped_args}"
end

#
# Tasks
#
task :clean do
  sh "git clean -fdx"
end

namespace :package do
  task download: [:clean] do
    fastlane "sdk"
  end

  task copy: [:clean] do
    fastlane "sdk", env: {FL_TEAK_SDK_SOURCE: '../teak-unity/'}
  end

  task :import do
    File.open('Assets/smcs.rsp', 'w') do |f|
      f.puts "-define:TEAK_NOT_AVAILABLE"
    end
    File.open('Assets/mcs.rsp', 'w') do |f|
      f.puts "-define:TEAK_NOT_AVAILABLE"
    end

    unity "-importPackage", "Teak.unitypackage"

    File.delete(*Dir.glob('Assets/smcs.rsp*'))
    File.delete(*Dir.glob('Assets/mcs.rsp*'))
  end
end

namespace :config do
  task all: [:id, :settings]

  task :id do
    unity "-executeMethod", "BuildPlayer.SetBundleId", PACKAGE_NAME
  end

  task :settings do
    template = File.read(File.join(PROJECT_PATH, 'Templates', 'TeakSettings.asset.template'))
    File.write(File.join(PROJECT_PATH, 'Assets', 'Resources', 'TeakSettings.asset'), Mustache.render(template, template_parameters))
  end
end

namespace :build do
  task :dependencies do
    unity "-buildTarget", "Android", "-executeMethod", "BuildPlayer.ResolveDependencies", quit: false, nographics: false
  end

  task android: [:dependencies] do
    FileUtils.rm_f('teak-unity-cleanroom.apk')

    template = File.read(File.join(PROJECT_PATH, 'Templates', 'AndroidManifest.xml.template'))
    File.write(File.join(PROJECT_PATH, 'Assets', 'Plugins', 'Android', 'AndroidManifest.xml'), Mustache.render(template, template_parameters))

    unity "-buildTarget", "Android", "-executeMethod", "BuildPlayer.Android", TARGET_API
  end

  task ios: ['ios:all']

  task :webgl do
    unity "-executeMethod", "BuildPlayer.WebGL"
  end
end

namespace :ios do
  task all: [:build, :postprocess, :fastlane]

  task :build do
    FileUtils.rm_f('teak-unity-cleanroom.ipa')
    FileUtils.rm_f('teak-unity-cleanroom.app.dSYM.zip')
    unity "-buildTarget", "iOS", "-executeMethod", "BuildPlayer.iOS"
  end

  task :postprocess do
    template = File.read(File.join(PROJECT_PATH, 'Templates', 'Unity-iPhone.entitlements.template'))
    File.write(File.join(PROJECT_PATH, 'iOSResources', 'Unity-iPhone', 'Unity-iPhone.entitlements'), Mustache.render(template, template_parameters))

    cp File.join(PROJECT_PATH, 'iOSResources', 'Unity-iPhone', 'Unity-iPhone.entitlements'),
       File.join(PROJECT_PATH, 'Unity-iPhone', 'Unity-iPhone', 'Unity-iPhone.entitlements')
    sh "ruby iOSResources/AddEntitlements.rb Unity-iPhone"
  end

  task :fastlane do
    sh 'bundle exec fastlane dev'
  end
end

namespace :deploy do
  task :ios do
    sh "aws s3 cp teak-unity-cleanroom.ipa s3://teak-build-artifacts/unity-cleanroom/teak-unity-cleanroom-`cat TEAK_VERSION`.ipa --acl public-read"
  end

  task :android do
    sh "aws s3 cp teak-unity-cleanroom.apk s3://teak-build-artifacts/unity-cleanroom/teak-unity-cleanroom-`cat TEAK_VERSION`.apk --acl public-read"
  end
end

namespace :install do
  task :ios do
    # # https://github.com/libimobiledevice/libimobiledevice/issues/510#issuecomment-347175312
    sh "ideviceinstaller -i teak-unity-cleanroom.ipa"
  end

  task :android do
    devicelist = %x[AndroidResources/devicelist].split(',').collect{ |x| x.chomp }
    devicelist.each do |device|
      adb = lambda { |*args| sh "adb -s #{device} #{args.join(' ')}" }

      begin
        adb.call "uninstall #{PACKAGE_NAME}"
      rescue
      end
      adb.call "install teak-unity-cleanroom.apk"
      adb.call "shell am start -n #{PACKAGE_NAME}/io.teak.sdk.wrapper.unity.TeakUnityPlayerActivity"
    end
  end

  task :webgl do
    sh "ruby -run -e httpd WebGlBuild -p 8000 &"
    sh "open http://localhost:8000"
    sh "fg"
  end
end
