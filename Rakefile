require "rake/clean"
require "shellwords"
require "mustache"
require "httparty"
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

UNITY_COMPILERS = [:gmsc, :smcs, :smcs, :mcs, :csc]
UNITY_HOME = ENV.fetch('UNITY_HOME', '/Applications/Unity-2017.1.0f3')
RVM_VARS = %w(GEM_HOME IRBRC MY_RUBY_HOME GEM_PATH)
PROJECT_PATH = Rake.application.original_dir
BUILD_TYPE = ENV.fetch('BUILD_TYPE', 'dev')
TARGET_API = ENV.fetch('TARGET_API', 26)
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

KMS_KEY = `aws kms decrypt --ciphertext-blob fileb://kms/store_encryption_key.key --output text --query Plaintext | base64 --decode`
CIRCLE_TOKEN = ENV.fetch('CIRCLE_TOKEN') { `openssl enc -md MD5 -d -aes-256-cbc -in kms/encrypted_circle_ci_key.data -k #{KMS_KEY}` }
FB_UPLOAD_TOKEN = ENV.fetch('FB_UPLOAD_TOKEN') { `openssl enc -md MD5 -d -aes-256-cbc -in kms/encrypted_fb_upload_token.data -k #{KMS_KEY}` }

FORCE_CIRCLE_BUILD_ON_FETCH = ENV.fetch('FORCE_CIRCLE_BUILD_ON_FETCH', false)

def unity?
  File.exist? "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity"
end

def ci?
  ENV.fetch('CI', false).to_s == 'true'
end

def add_unity_log_to_artifacts
  cp('unity.log', "#{Rake.application.current_task.name.sub(':', '-')}.unity.log") unless $!.nil?
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
  if ci?
    add_unity_log_to_artifacts
    Rake::Task["unity_license:release"].invoke unless Rake.application.current_task.name.start_with?('unity_license')
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
  sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -logFile #{PROJECT_PATH}/unity.log#{quit ? ' -quit' : ''}#{nographics ? ' -nographics' : ''} -batchmode -projectPath #{PROJECT_PATH} #{escaped_args}", verbose: false
  ensure
    return unless ci?
    add_unity_log_to_artifacts
end

def fastlane(*args, env:{})
  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "#{env.map{|k,v| "#{k}='#{v}'"}.join(' ')} bundle exec fastlane #{escaped_args}", verbose: false
end

def build_and_fetch(version, extension)
  filename = "teak-unity-cleanroom-#{version}.#{extension}"
  if FORCE_CIRCLE_BUILD_ON_FETCH.to_s == 'true' || %x[aws s3 ls s3://teak-build-artifacts/unity-cleanroom/ | grep #{filename}].empty?

    # Kick off a CircleCI build for that version
    puts "Version #{version} not found in S3, triggering a CircleCI build..."
    response = HTTParty.post("https://circleci.com/api/v1.1/project/github/GoCarrot/teak-unity-cleanroom/tree/master?circle-token=#{CIRCLE_TOKEN}",
                              {
                                body: {
                                  build_parameters:{
                                    FL_TEAK_SDK_VERSION: version
                                  }
                                }.to_json,
                                headers: {
                                  'Content-Type' => 'application/json',
                                  'Accept' => 'application/json'
                                }
                              })
    build_num = response['build_num']
    previous_build_time_ms = response['previous_successful_build']['build_time_millis']
    previous_build_time_sec = previous_build_time_ms * 0.001

    # Sleep for 3/4 of the previous build time
    puts "Previous successful build took #{previous_build_time_sec} seconds."
    puts "Waiting #{previous_build_time_sec * 0.90} seconds..."
    sleep(previous_build_time_sec * 0.90)

    loop do
      # Get status
      response = HTTParty.get("https://circleci.com/api/v1.1/project/github/GoCarrot/teak-unity-cleanroom/#{build_num}?circle-token=#{CIRCLE_TOKEN}",
                              {format: :json})
      break unless response['status'] == "running"
      puts "Build status: #{response['status']}, checking again in #{previous_build_time_sec * 0.1} seconds"
      sleep(previous_build_time_sec * 0.1)
    end
  end
  sh "aws s3 sync s3://teak-build-artifacts/unity-cleanroom/ . --exclude '*' --include '#{filename}'"
  filename
end

#
# Tasks
#
task :clean do
  sh "git clean -fdx" unless ci?
end

task :warnings_as_errors do
  UNITY_COMPILERS.each do |compiler|
    File.open("Assets/#{compiler}.rsp", 'w') do |f|
      f.puts "-warnaserror+"
    end
  end
end

namespace :unity_license do
  task :acquire do
    # return unless ci?
    unity "-executeMethod", "BuildPlayer.CheckLicense", PACKAGE_NAME, nographics: false
  end

  task :release do
    return unless ci?
    sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -batchmode -quit -returnlicense", verbose: false rescue nil
    puts "Released Unity license..."
  end
end

namespace :package do
  task download: [:clean] do
    fastlane "sdk"
  end

  task copy: [:clean] do
    fastlane "sdk", env: {FL_TEAK_SDK_SOURCE: "#{PROJECT_PATH}/../teak-unity/"}
  end

  task :import do
    UNITY_COMPILERS.each do |compiler|
      File.open("Assets/#{compiler}.rsp", 'w') do |f|
        f.puts "-define:TEAK_NOT_AVAILABLE"
      end
    end

    unity "-importPackage", "Teak.unitypackage"

    File.delete(*Dir.glob('Assets/*.rsp*'))
  end
end

namespace :config do
  task all: [:id, :settings, :apple_team_id]

  task :id do
    unity "-executeMethod", "BuildPlayer.SetBundleId", PACKAGE_NAME
  end

  task :apple_team_id do
    unity "-executeMethod", "BuildPlayer.SetAppleTeamId", "7FLZTACJ82" # TODO: Pull from Fastlane
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

  task android: [:dependencies, :warnings_as_errors] do
    FileUtils.rm_f('teak-unity-cleanroom.apk')

    template = File.read(File.join(PROJECT_PATH, 'Templates', 'AndroidManifest.xml.template'))
    File.write(File.join(PROJECT_PATH, 'Assets', 'Plugins', 'Android', 'AndroidManifest.xml'), Mustache.render(template, template_parameters))

    unity "-buildTarget", "Android", "-executeMethod", "BuildPlayer.Android", TARGET_API
  end

  task ios: ['ios:all']

  task webgl: [:warnings_as_errors] do
    unity "-executeMethod", "BuildPlayer.WebGL"
    template = File.read(File.join(PROJECT_PATH, 'Templates', 'index.html.template'))
    FileUtils.mkdir_p(File.join(PROJECT_PATH, 'WebGLBuild'))
    File.write(File.join(PROJECT_PATH, 'WebGLBuild', 'index.html'), Mustache.render(template, template_parameters))
    sh '(cd WebGLBuild/; zip -r ../teak-unity-cleanroom.zip .)'
  end
end

namespace :ios do
  task all: [:fastlane_match, :build, :postprocess, :fastlane]

  task :fastlane_match do
    sh 'bundle exec fastlane match development' if ci?
  end

  task build: [:warnings_as_errors] do
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

  task :webgl do
    sh "curl -X POST https://graph-video.facebook.com/#{TEAK_CREDENTIALS[BUILD_TYPE][:teak_app_id]}/assets -F 'access_token=#{FB_UPLOAD_TOKEN}' -F 'type=UNITY_WEBGL' -F 'asset=@./teak-unity-cleanroom.zip' -F 'comment=#{`cat TEAK_VERSION`}'"
  end
end

namespace :install do
  task :ios, [:version] do |t, args|
    ipa_path = args[:version] ? build_and_fetch(args[:version], :ipa) : "teak-unity-cleanroom.ipa"

    begin
      sh "ideviceinstaller --uninstall #{PACKAGE_NAME}"
    rescue
    end
    # https://github.com/libimobiledevice/libimobiledevice/issues/510#issuecomment-347175312
    sh "ideviceinstaller --install #{ipa_path}"
  end

  task :android, [:version] do |t, args|
    apk_path = args[:version] ? build_and_fetch(args[:version], :apk) : "teak-unity-cleanroom.apk"

    devicelist = %x[AndroidResources/devicelist].split(',').collect{ |x| x.chomp }
    devicelist.each do |device|
      adb = lambda { |*args| sh "adb -s #{device} #{args.join(' ')}" }

      begin
        adb.call "uninstall #{PACKAGE_NAME}"
      rescue
      end
      adb.call "install #{apk_path}"
      adb.call "shell am start -n #{PACKAGE_NAME}/io.teak.sdk.wrapper.unity.TeakUnityPlayerActivity"
    end
  end

  task :webgl do
    sh "ruby -run -e httpd WebGlBuild -p 8000 &"
    sh "open http://localhost:8000"
    sh "fg"
  end
end
